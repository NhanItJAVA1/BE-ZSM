namespace BE_ZSM.Exceptions
{
    public class UnauthorizedException : AppException
    {
        private string v;
        public UnauthorizedException(string message, string errorCode) : base(message, 401, errorCode)
        {
        }
    }
}
