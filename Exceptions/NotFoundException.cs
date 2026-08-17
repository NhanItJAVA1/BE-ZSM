namespace BE_ZSM.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message, string errorCode) : base(message, 404, errorCode)
        {
        }
    }
}
