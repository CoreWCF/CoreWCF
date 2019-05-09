using System;
using System.Reflection;

namespace Microsoft.ServiceModel.Description
{
    internal static class TaskOperationDescriptionValidator
    {
        internal static void Validate(OperationDescription operationDescription, bool isForService)
        {
            MethodInfo taskMethod = operationDescription.TaskMethod;
            if (taskMethod != null)
            {
                if (isForService)
                {
                    // no other method (sync, async) is allowed to co-exist with a task-based method on the server-side.
                    EnsureNoSyncMethod(operationDescription);
                    EnsureNoBeginEndMethod(operationDescription);
                }
                else
                {
                    // no out/ref parameter is allowed on the client-side.
                    EnsureNoOutputParameters(taskMethod);
                }

                EnsureParametersAreSupported(taskMethod);
            }
        }

        private static void EnsureNoSyncMethod(OperationDescription operation)
        {
            if (operation.SyncMethod != null)
            {
                string method1Name = operation.TaskMethod.Name;
                string method2Name = operation.SyncMethod.Name;
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CannotHaveTwoOperationsWithTheSameName3, method1Name, method2Name, operation.DeclaringContract.ContractType)));
            }
        }

        private static void EnsureNoBeginEndMethod(OperationDescription operation)
        {
            if (operation.BeginMethod != null)
            {
                string method1Name = operation.TaskMethod.Name;
                string method2Name = operation.BeginMethod.Name;
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.CannotHaveTwoOperationsWithTheSameName3, method1Name, method2Name, operation.DeclaringContract.ContractType)));
            }
        }

        private static void EnsureParametersAreSupported(MethodInfo method)
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type parameterType = parameter.ParameterType;
                if ((parameterType == ServiceReflector.CancellationTokenType) ||
                    (parameterType.GetTypeInfo().IsGenericType && parameterType.GetGenericTypeDefinition() == ServiceReflector.IProgressType))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.TaskMethodParameterNotSupported, parameterType)));
                }
            }
        }

        private static void EnsureNoOutputParameters(MethodInfo method)
        {
            if (ServiceReflector.HasOutputParameters(method, false))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.TaskMethodMustNotHaveOutParameter));
            }
        }
    }

}