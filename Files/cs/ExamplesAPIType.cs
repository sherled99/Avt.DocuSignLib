using System.ComponentModel;
namespace Avt.DocuSignLib.Files.cs
{
    public enum ExamplesAPIType
    {
        [Description("Reg")]
        Rooms = 0,

        [Description("eg")]
        ESignature = 1,

        [Description("ClickEg")]
        Click = 2,

        [Description("monitorExample")]
        Monitor = 3,

        [Description("Aeg")]
        Admin = 4,
    }

    public static class ExamplesAPITypeExtensions
    {
        public static string ToKeywordString(this ExamplesAPIType val)
        {
            DescriptionAttribute[] attributes = (DescriptionAttribute[])val
               .GetType()
               .GetField(val.ToString())
               .GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : string.Empty;
        }
    }
}
