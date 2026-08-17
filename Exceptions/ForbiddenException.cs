namespace BE_ZSM.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message, string errorCode) : base(message, 403, errorCode)
        {
        }
    }
}
