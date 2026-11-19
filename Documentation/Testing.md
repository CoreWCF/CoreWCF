# Getting started with testing

Most tests can be run without any special commands or processes. Running the following command from the project's root directory will restore packages, build the solution, and run the full CoreWCF test suite:

    dotnet test src/CoreWCF.sln

## Running integration tests

Some integration tests are dependent on external services (e.g. `CoreWCF.RabbitMQ.Tests` require access to a RabbitMQ host), thereby requiring additional setup before those tests can run successfully. The steps below will create local containers hosting all dependent services required by the integration tests.

1. [Install docker v19.03.0+](https://docs.docker.com/engine/install/) to your development environment via Docker Desktop or the docker engine
2. From the root directory of the CoreWCF repo, run the following command to start the service containers:

        docker compose up -d
		
3. Run the tests as usual, using your IDE or running the following command from the command line:

        dotnet test src/CoreWCF.sln

4. After running the tests, run the following command to stop the service containers and clean up all related artifacts (images, networks, volumes, etc):

        docker compose down --rmi -v

    Alternatively, the following command will stop the service containers while preserving container artifacts:

        docker compose stop

## Platform specific tests

Note: Some unit tests are set to only run on specific platforms (i.e. Windows-only or Linux-only) based on the features available to those operating systems. 

### Named-pipe streaming regression tests

On Windows, run `CoreWCF.NetNamedPipe.Tests` to exercise streamed response delivery:

```shell
dotnet test src/CoreWCF.NetNamedPipe/tests/CoreWCF.NetNamedPipe.Tests.csproj -c Release -f net10.0
```

`StreamingResponseTests` checks empty responses and tails around segment boundaries,
delivery of a small prefix while the service is paused, bounded producer read-ahead
with one or multiple stalled readers, resuming those readers, and aborting a stalled
response. It also checks streamed delivery with transport security enabled.
The slow-reader tests verify that producer progress stops below 512 KiB per response;
this is a bound for the test workload, including encoder and transport buffering,
not a general limit on application-provided write sizes.

The named-pipe transport uses 64 KiB minimum output segments and a default
256 KiB output pause threshold (128 KiB resume), independently of the native pipe's
`ConnectionBufferSize`. Larger configured connection buffers also raise the output
threshold. These are allocation and backpressure settings, not flush thresholds:
small responses and partial segments are still flushed immediately. Input and
native pipe buffer sizes are unchanged.
The 64 KiB segment size keeps ordinary pooled backing arrays below the default
large-object heap threshold; it is a minimum allocation size, not a hard cap on
every possible allocation.
