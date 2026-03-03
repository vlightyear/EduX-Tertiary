using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Payments
{
    public class PayBossAuthResponse
    {
        [JsonPropertyName("tokenType")]  public string TokenType { get; set; } = "";
        [JsonPropertyName("token")]      public string Token     { get; set; } = "";
        [JsonPropertyName("expiresIn")]  public int    ExpiresIn { get; set; }
    }

    public class PayBossMobileResponse
    {
        [JsonPropertyName("status")]        public string Status        { get; set; } = "";
        [JsonPropertyName("message")]       public string Message       { get; set; } = "";
        [JsonPropertyName("transactionID")] public string TransactionID { get; set; } = "";
    }

    public class PayBossCardResponse
    {
        [JsonPropertyName("status")]        public string Status        { get; set; } = "";
        [JsonPropertyName("message")]       public string Message       { get; set; } = "";
        [JsonPropertyName("transactionID")] public string TransactionID { get; set; } = "";
        [JsonPropertyName("redirectUrl")]   public string RedirectUrl   { get; set; } = "";
    }

    public class PayBossStatusResponse
    {
        [JsonPropertyName("status")]                           public string  Status                           { get; set; } = "";
        [JsonPropertyName("statusCode")]                       public string  StatusCode                       { get; set; } = "";
        [JsonPropertyName("message")]                          public string  Message                          { get; set; } = "";
        [JsonPropertyName("transactionID")]                    public string  TransactionID                    { get; set; } = "";
        [JsonPropertyName("serviceProviderRef")]               public string? ServiceProviderRef               { get; set; }
        [JsonPropertyName("serviceProviderStatusDescription")] public string? ServiceProviderStatusDescription { get; set; }
    }

    public class StudentCardPaymentViewModel
    {
        [Required] public decimal Amount               { get; set; }
        [Required] public string  PhoneNumber          { get; set; } = "";
        [Required] public string  FirstName            { get; set; } = "";
        [Required] public string  LastName             { get; set; } = "";
        [Required][EmailAddress]
                   public string  Email                { get; set; } = "";
        [Required] public string  Address              { get; set; } = "";
                   public string  City                 { get; set; } = "Lusaka";
                   public string  Country              { get; set; } = "ZM";
                   public string  PostalCode           { get; set; } = "10101";
                   public string  Province             { get; set; } = "Lusaka";
        [Required] public string  TransactionReference { get; set; } = "";
    }
}
