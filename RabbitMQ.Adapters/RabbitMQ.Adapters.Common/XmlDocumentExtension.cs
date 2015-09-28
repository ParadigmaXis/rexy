using System.Xml;

namespace RabbitMQ.Adapters.Common {
    public static class XmlDocumentExtension {
        public static bool IsWsdl(this System.Xml.Linq.XDocument document) {
            var name = document.Root.Name;
            return name.NamespaceName == "http://schemas.xmlsoap.org/wsdl/" && name.LocalName == "definitions";
        }

        public static bool IsSoapEnvelope(this System.Xml.Linq.XDocument document) {
            var name = document.Root.Name;
            return name.NamespaceName == "http://schemas.xmlsoap.org/soap/envelope/" && name.LocalName == "Envelope";
        }
    }
}
