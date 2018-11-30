namespace TelegramToVCFExporter
{
    public class Contact
    {
        public string FirstAndLastName { get; set; }
        public string PhoneNumber { get; set; }
        public string VCF { get; set; }

        public Contact(string firstAndLastName, string phoneNumber)
        {
            FirstAndLastName = firstAndLastName;
            PhoneNumber = phoneNumber;
            VCF =
                $"BEGIN:VCARD" +
                $"\nVERSION:2.1" +
                $"\nN;CHARSET=UTF-8:{this.FirstAndLastName}" +
                $"\nTEL;CELL:{this.PhoneNumber}" +
                $"\nEND:VCARD";
        }

        public override string ToString()
        {
            return
                $"{nameof(FirstAndLastName)}: {FirstAndLastName}, {nameof(PhoneNumber)}: {PhoneNumber}, {nameof(VCF)}:\n{VCF}\n";
        }
    }
}