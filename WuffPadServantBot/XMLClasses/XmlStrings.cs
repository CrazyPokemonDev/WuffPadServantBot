using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace WuffPadServantBot.XMLClasses
{
    [XmlRoot("strings")]
    public class XmlStrings
    {
        public XmlStrings()
        {
            Strings = new ObservableCollection<XmlString>();
            Language = new XmlLanguage();
        }
        [XmlElement("language")]
        public XmlLanguage Language { get; set; }
        [XmlElement("string")]
        public ObservableCollection<XmlString> Strings { get; set; }
    }
}
