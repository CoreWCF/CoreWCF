namespace CoreWCF
{
    public struct TryAsyncResult
    {
        public static TryAsyncResult<TResult> FromResult<TResult>(TResult result)
        {
            return new TryAsyncResult<TResult>(result);
        }

        public static TryAsyncResult<TResult> Failed<TResult>()
        {
            return TryAsyncResult<TResult>.FailedResult;
        }
    }

    public struct TryAsyncResult<TResult>
    {
        public static TryAsyncResult<TResult> FailedResult = new TryAsyncResult<TResult>();

        public TryAsyncResult(TResult result)
        {
            Success = true;
            Result = result;
        }

        public bool Success { get; private set; }
        public TResult Result { get; private set; }
    }
}