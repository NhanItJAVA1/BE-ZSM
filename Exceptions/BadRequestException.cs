namespace BE_ZSM.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string message, string errorCode) : base(message, 400, errorCode)
        {
        }
    }
}
