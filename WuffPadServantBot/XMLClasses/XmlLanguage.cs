using System.Xml.Serialization;

namespace WuffPadServantBot.XMLClasses
{
    public class XmlLanguage
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("base")]
        public string Base { get; set; }
        [XmlAttribute("variant")]
        public string Variant { get; set; }
        [XmlAttribute("owner")]
        public string Owner { get; set; }
    }
}
