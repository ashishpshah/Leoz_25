namespace Leoz_25
{
    public class ChangePassword: EntitiesBase
    {
        public long Id { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
