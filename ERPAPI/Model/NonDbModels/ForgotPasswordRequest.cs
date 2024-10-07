namespace ERPAPI.Model.NonDbModels
{
    public class ForgotPasswordRequest
    {
        public string UserName { get; set; }
        public string SecurityAnswer1 { get; set; }
        public string SecurityAnswer2 { get; set; }
        public string NewPassword { get; set; }
    }
}
