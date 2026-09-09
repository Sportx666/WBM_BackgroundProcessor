namespace WBM_BackgroundProcessor.Models.DataHolders
{
    public class AuthenticationResponse
    {
        public string? APITransactionReference { get; set; }

        public string Token { get; set; }
        public DateTime TokenExpiry { get; set; }
        public string AccessList { get; set; }
    }
    public class Authenticate
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public string CallReference { get; set; }
    }
}
