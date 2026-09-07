namespace BE_ZSM.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message, string errorCode) : base(message, 401, errorCode)
        {
        }
    }
}
