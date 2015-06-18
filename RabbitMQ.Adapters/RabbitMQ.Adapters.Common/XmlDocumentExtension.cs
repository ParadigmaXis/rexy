using System.Xml;

namespace RabbitMQ.Adapters.Common {
    public static class XmlDocumentExtension {
        public static bool IsWsdl(this XmlDocument document) {
            return document.DocumentElement.NamespaceURI == "http://schemas.xmlsoap.org/wsdl/" && document.DocumentElement.LocalName == "definitions";
        }

        public static bool IsSoapMessage(this XmlDocument document) {
            return document.DocumentElement.NamespaceURI == "http://schemas.xmlsoap.org/soap/envelope/" && document.DocumentElement.LocalName == "Envelope";
        }
    }
}
