# Objective 
The objective of this document is to provide details on how to build a generic queue transport inside CoreWCF so that it can support various queue bindings. This document is based on the discussion outlined here: https://github.com/CoreWCF/CoreWCF/issues/164 .

# Problem Overview
When WCF was built, there were not many queuing technologies and there was no concept of the cloud. With multiple cloud providers supporting queuing technologies and many queuing technologies widely available, we wanted to create a generic transport that would allow CoreWCF to support a variety of queuing mechanisms. 

# Class Diagram
![CoreWCF Generic Queue](/Documentation/DesignDocs/generic_queue_class_diagram.png?raw=true)

# Class Details
#### IQueueMiddlewareBuilder and QueueMiddlewareBuilder
- `IQueueMiddlewareBuilder` provides signatures for 2 important methods, `Use` and `Build`. 
```csharp
internal interface IQueueMiddlewareBuilder 
{ 
   IServiceProvider QueueServices { get; set; } 
   IDictionary<string, object> Properties { get; } 
   IQueueMiddlewareBuilder Use(Func<QueueMessageDispatcherDelegate, QueueMessageDispatcherDelegate> middleware); 
   QueueMessageDispatcherDelegate Build(); 
} 
```
- `Use` adds middleware delegates to the Queue pipeline 
- `Build` builds the delegate used by the application to process requests received from the queue.
#### QueueMiddleWareBuilderExtension
- The `QueueMiddleWareBuilderExtension` class defines the `UseMiddleware` method which searches the provided `TMiddleware` generic parameter type for an `InvokeAsync` method with a method signature that matches the `QueueMessageDispatcherDelegate` delegate. It expects a constructor to exist which takes an instance of a `QueueMessageDispatcherDelegate` delegate as the first parameter, and any remaining parameters to match the optional additional args. The `QueueMessageDispatcherDelegate` delegate passed to the constructor is the next middleware to call when the middleware doesn't terminate the dispatch chain. This is similar to the NetTcp class `UseMiddlewareFramingConnectionHandshakeExtensions`, and the ASP.NET Core `UseMiddlewareExtensions` class.
```csharp
public static class QueueMiddleWareBuilderExtension
{
    internal const string InvokeAsyncMethodName = "InvokeAsync";
    
    public static IQueueMiddlewareBuilder UseMiddleware<TMiddleware>(this IQueueMiddlewareBuilder app, params object[] args);
    
    public static IQueueMiddlewareBuilder UseMiddleware(this IQueueMiddlewareBuilder app, Type middleware, params object[] args);
}
```
#### QueueServiceCollectionExtension
- `QueueServiceCollectionExtension` is the service collection extension which stiches all the queue middleware pipeline and `QueuePollingService`, which will be called during service startup for the queuing mechanism to work.
. e:g:- 
```csharp
internal sealed class Startup
{
    var builder = WebApplication.CreateBuilder();
    builder.Services.AddServiceModelServices();
    builder.Services.AddServiceModelMetadata();
    builder.Services.AddQueueTransport();
    var app = builder.Build()
    app.UseServiceModel(serviceBuilder =>
    {
        serviceBuilder.AddService<Service>();
        serviceBuilder.AddServiceEndpoint<Service, IService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport), "/Service.svc");
    });
    app.Run();
}
public static class QueueServiceCollectionExtension
{
    public static IServiceCollection AddQueueTransport(this IServiceCollection services,
        Action<QueueOptions> configureQueues = null)
    {
        services.AddTransient<IQueueMiddlewareBuilder, QueueMiddlewareBuilder>();
        if (configureQueues != null)
        {
            services.Configure(configureQueues);
        }
        services.AddSingleton<QueueHandShakeMiddleWare>();
        services.AddHostedService<QueuePollingService>();
        services.AddTransient<QueueInputChannel>();
        return services;
    }
}

 ```
 - It adds an implementation of `IQueueMiddlewareBuilder` (`QueueMiddlewareBuilder`) to DI so that a Queue based binding is able to build the transport layer stack by adding middleware.
 - It adds other needed components to DI such as `QueueHandShakeMiddleWare` , `QueuePollingService` and `QueueInputChannel` which are necessary to build and run a Queue based service.
#### QueueHandShakeMiddleWare
- The `QueueHandShakeMiddleWare` constructor attaches 2 middleware components `QueueFetchMessage` and `QueueProcessMessage`.
- The Build method builds the middleware pipeline and returns delegate `QueueMessageDispatcherDelegate`.
```csharp
internal class QueueHandShakeMiddleWare
{
                
    private readonly IQueueMiddlewareBuilder _queueMiddlewareBuilder;
    private readonly IServiceProvider _services;
    private readonly IServiceScopeFactory _servicesScopeFactory;

    public QueueHandShakeMiddleWare(
        IServiceProvider services,
        IServiceScopeFactory servicesScopeFactory, IQueueMiddlewareBuilder queueBuilder)
    {
        _services = services;
        _servicesScopeFactory = servicesScopeFactory;
        _queueMiddlewareBuilder = InitQueueMiddleWare(queueBuilder);
    }

    private IQueueMiddlewareBuilder InitQueueMiddleWare(IQueueMiddlewareBuilder queueBuilder)
    {
        queueBuilder.UseMiddleware<QueueFetchMessage>();
        queueBuilder.UseMiddleware<QueueProcessMessage>();
        return queueBuilder;
    }

    public QueueMessageDispatcherDelegate Build()
    {
        return _queueMiddlewareBuilder.Build();
    }
       
}
```
#### IQueueTransport, QueueBaseTransportBindingElement , QueueTransportContext and QueueTransportPump
- `IQueueTransport` defines a single method, `ReceiveQueueMessageContextAsync`. The `QueuePollingService` will call this method to fetch the next incoming queue message. 
```csharp
   public interface IQueueTransport
    {
        Task<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken);
    }
```
- `QueueBaseTransportBindingElement` is an abstract class with `BuildQueueTransportPump` as an abstract method
- Each Queue transport binding element must extend `QueueBaseTransportBindingElement` and implement `BuildQueueTransportPump`.
```csharp
    public abstract class QueueBaseTransportBindingElement : TransportBindingElement
    {
        protected QueueBaseTransportBindingElement()
        {
        }

        protected QueueBaseTransportBindingElement(QueueBaseTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public virtual int MaxPendingReceives { get { return 1; } }
        public abstract QueueTransportPump BuildQueueTransportPump(BindingContext context);

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            return (typeof(TChannel) == typeof(IInputChannel));
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            return innerDispatcher;
        }

    }
 ```
- The concrete implementation of `IQueueTransport` will be used by CoreWCF to retrieve messages from the queue storage service. 
- When `ReceiveQueueMessageContextAsync` method is called, it returns an instance of `QueueMessageContext` object.
- `ServiceDispatcher`, `QueueBaseTransportBindingElement`, `QueueMessageDispatcherDelegate`, and `QueueTransportPump` are all stored in `QueueTransportContext`. 
-`QueueTransportPump` is an abstract class with `StartPumpAsync` and `StopPumpAsync` Task returning methods. 
- Any queue transport which is using push mechanism (e:g:- RabbitMQ) needs to implement this class.
- `QueueTransportPump` also provides a default implementation called `DefaultQueueTransportPump` to be used by Queue transport, which implements a pull mechanism to fetch messages.
```csharp
    public abstract class QueueTransportPump
    {
        public abstract Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token);
        public abstract Task StopPumpAsync(CancellationToken token);
        public static QueueTransportPump CreateDefaultPump(IQueueTransport queueTransport)
        {
            return new DefaultQueueTransportPump(queueTransport);
        }
    }

    internal class DefaultQueueTransportPump : QueueTransportPump
    {
        private readonly IQueueTransport _transport;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<Task> _tasks = new List<Task>();

        public DefaultQueueTransportPump(IQueueTransport queueTransport)
        {
            _transport = queueTransport;
        }

        public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token);
        public override Task StopPumpAsync(CancellationToken token);
        private async Task FetchAndProcessAsync(QueueTransportContext queueTransportContext, CancellationToken cancellationToken);
    }
```
#### QueuePollingService
- `QueuePollingService` is the center for queue implementation. It starts when the host starts and continues fetching messages until the host is stopped. 
- At the initialization of the class, 
```csharp
private async Task Init()
{
    IDispatcherBuilder dispatcherBuilder = _services.GetRequiredService<IDispatcherBuilder>();
    var optnVal = _options.Value ?? new QueueOptions();
    foreach (Type serviceType in _serviceBuilder.Services)
    {
        List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(serviceType);
        foreach (IServiceDispatcher dispatcher in dispatchers)
        {
            if (dispatcher.BaseAddress == null)
                    {
                continue;
            }

            BindingElementCollection be = dispatcher.Binding.CreateBindingElements();
            QueueBaseTransportBindingElement queueTransportBinding = be.Find<QueueBaseTransportBindingElement>();
            var msgEncBindingelement = be.Find<MessageEncodingBindingElement>();
            if (queueTransportBinding == null)
            {
                continue;
            }
            IServiceDispatcher _serviceDispatcher = null;
            var _customBinding = new CustomBinding(dispatcher.Binding);
            var parameters = new BindingParameterCollection();
            parameters.Add(optnVal);
            parameters.Add(_services);
            if (_customBinding.CanBuildServiceDispatcher<IInputChannel>(parameters))
            {
                _serviceDispatcher = _customBinding.BuildServiceDispatcher<IInputChannel>(parameters, dispatcher);
            }
            parameters.Add(_serviceDispatcher);
            BindingContext bindingContext = new BindingContext(_customBinding, parameters);
            QueueTransportPump queuePump = queueTransportBinding.BuildQueueTransportPump(bindingContext);

            _queueTransportContexts.Add(new QueueTransportContext
            {
                QueuePump = queuePump,
                 ServiceDispatcher = _serviceDispatcher,
                 QueueBindingElement = queueTransportBinding,
                 MessageEncoderFactory = msgEncBindingelement.CreateMessageEncoderFactory(),
                 QueueHandShakeDelegate = _queueHandShakeMiddleWare.Build()
            });

        }
    }

}
```
- for each service type (which is fetched from DI), service dispatcher is created.
- for each service dispatcher, validation is made to ensure the service dispatcher binding element collection property has a transport binding element of type `QueueBaseTransportBindingElement`.
- using the `QueueBaseTransportBindingElement` which is fetched from above step, `BuildQueueTransportPump` method is called to get the `QueueTransportPump`.
  - In the end, for every service dispatcher, a `QueueTransportContext` object is created which stores objects of type `QueueTransportPump`, `ServiceDispatcher`, `QueueBaseTransportBindingElement`,`MessageEncoderFactory` and `QueueHandShakeDelegate`.
- Since `QueuePollingService` implements `IHostingService`, it implements the `StartAsync` and `StopAsync` methods.
- `StartAsync` is called by WebHost and it performs the below task.
```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    var tasks = _queueTransportContexts.Select(_queueTransport => StartFetchingMessage(_queueTransport));
    await Task.WhenAll(tasks);
}

private async Task StartFetchingMessage(QueueTransportContext queueTransport)
{
    await queueTransport.QueuePump.StartPumpAsync(queueTransport, CancellationToken.None);
}

```
- For StartAsync
  - First we create a list of tasks`StartFetchingMessage` for each `QueueTransportContext` (populated during Init).
  - `QueueTransportPump` from `QueueTransportContext` is used by each `StartFetchingMessage` to call the `StartPumpAsync` method.
  - If the `QueueTransportPump` is based on a push message mechanism, the provided implementation waits until a message is received and invokes service as soon as the message is received.
  - If the `QueueTransportPump` is using `DefaultQueueTransportPump`, it creates multiple concurrent tasks to invoke `FetchAndProcessAsync` method based on concurrency level.
  - The `FetchAndProcessAsync` method of QueueTransport (concrete transport) calls `ReceiveQueueMessageContextAsync` to retrieve the message and pass the handle to the queue middleware for processing. 
- StopAsync cancels all tasks and, as a result, the fetching of messages is stopped.
#### QueueFetchMessage
- `QueueFetchMessage` is the first queue middleware which is invoked as soon as a message is fetched from the queue.
- The `InvokeAsync` method reads messages from `Pipereader` and encodes them into a concrete `Message` objet and puts them back to `QueueMessageContext` and invokes the next delegate , which is `QueueProcessMessage`.
#### QueueProcessMessage
- Invokes the `DispatchAsync` method of the channel dispatcher by passing `QueueMessageContext` object and ending the queue middleware pipeline and transitions to the dispatch pipeline.
- As a part of `DispatchAsync`, the service operation is called, processing of messages is done, eventually `QueueMessageContext` (inherits `RequestContext`) `ReplyAsync` method is called.
- The `ReplyAsync` mechanism is used to inform the transport implementation that message processing has finished with a status of success of failure. It signals the status result by invoking a `DispatchResultHandler` delegate. 
- If a queue provider requires completion notification to support timeouts or other features, they can do so by setting the `DispatchResultHandler` delegate and taking action accordingly.. 
- The reply message sent via `DispatchResultHandler` could be a fault message, which will signal a failure to process the message, and can be used to provide an error message back to the queue provider.

# Workflow Diagram
![CoreWCF Generic Queue](/Documentation/DesignDocs/corewcf_queue.png?raw=true)


 - During the server registrations, the `QueuePollingService` initializes the service dispatcher and gets the concrete transport from each queue layer transport.
 - When the ASP.NET Core WebHost starts, it calls `IHostedService.StartAsync` on the `QueuePollingService`, creates a list of tasks (`StartFetchingMessage`) for each transport and runs them in a loop.
 - Each task again creates a list of subtasks(`FetchAndProcessAsync`) based on the `QueueTransport`  to retrieve message context(`QueueMessageContext`) , until Webhost calls `QueuePollingService.StopAsync` as the mechanism that it will be stopped.
 - The fetched `QueueMessageContext` is passed to the queue middleware. The first queue middleware layer reads the message bytes from a `PipeReader` and decodes them to a Message object.
 - The Message object is put into the `QueueMessageContext` object and passed to the next layer, `QueueProcessMessage`.
 - The `QueueProcessMessage` reads the message using the binding provided `MessageEncoder` and invokes `DispatchAsync` on the `IServiceChannelDispatcher` to call the service based on the binding and message header information.
 - Once the request is processed, `QueueMessageContext` sends the result to queue transport implementation to notify that the message is being processed. It will pass a result status (failed/success) and an optional fault Message so that the queue transport implementation can take appropriate actions.

# Notifying Client
- Every internal transport when constructing a `QueueMessageContext` via `ReceiveQueueMessageContextAsync` method, they can pass a delegate in the form of `DispatchResultHandler` .
- Once a message is processed and service is invoked, the control comes to `ReplyAsync` method of `QueueMessageContext`.If `DispatchResultHandler` has been set, it's invoked by passing status of service invocation and full `QueueMessageContext`
- Internal transport can act based on that to notify client or put into dead letter queue or trigger any custom fuctions.
```csharp
    public class QueueMessageContext : RequestContext
    {

        public override Task ReplyAsync(Message message)
        {
           if (DispatchResultHandler != null)
            {
                if (message!=null && message.IsFault) DispatchResultHandler(QueueDispatchResult.Failed, this);
                else DispatchResultHandler(QueueDispatchResult.Processed, this);
            }
            return Task.CompletedTask;
        }
        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            return ReplyAsync(message);

        }
        public override Task CloseAsync() { return Task.CompletedTask; }
        public override Task CloseAsync(CancellationToken token) { return Task.CompletedTask; }

        public Action<QueueDispatchResult, QueueMessageContext> DispatchResultHandler { get; set; }

    }
```
# Basic Design Code
https://github.com/birojnayak/CoreWCF/tree/DmitryModifiedCommit 
