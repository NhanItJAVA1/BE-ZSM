namespace BE_ZSM.Exceptions
{
    public class AppException : Exception
    {
        public int StatusCode { get; set; }
        public string ErrorCode { get; set; }

        public AppException(string message, int statusCode, string errorCode) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
}
