using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Reflection;

using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;


namespace OpenSim.Server.Handlers.WxServerHandler
{
    public class WxPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IWxService m_WxService;
        // Service the requests here
        public WxPostHandler(IWxService service) : base("POST", "/Wx")
        {
            m_WxService = service;
        }

        public override byte[] Handle(string path, Stream requestData, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {

            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            try
            {
                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();
                string method = request["METHOD"].ToString();

                switch (method)
                {
                    case "testing":

                        return TestResponse(request);

                    case "get_user_info":
                        return GetUserInfo(request);

                    case "deregister":
                        //return Deregister(request);
                        return FailureResult();

                }
                m_log.DebugFormat("[Wx HANDLER]: unknown method {0} request {1}", method.Length, method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[Wx HANDLER]: Exception {0}", e);
            }

            return FailureResult();

        }

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return DocToBytes(doc);
        }

        private byte[] FailureResult()
        {
            return FailureResult(String.Empty);
        }

        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new XmlDocument();
            
            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");
            
            doc.AppendChild(xmlnode);
            
            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");
            
            doc.AppendChild(rootElement);
            
            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));
            
            rootElement.AppendChild(result);
            
            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));
            
            rootElement.AppendChild(message);
            
            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();
            
            return ms.ToArray();
        }

        private byte[] GetUserInfo (Dictionary<string, object> request)
        {
            UUID id = UUID.Zero;
            UserAccount d = null;

            if ( request.ContainsKey("user_id"))
            {
                if (UUID.TryParse(request["user_id"].ToString(), out id))
                {
                    d = m_WxService.GetUserData(id);
                }
                else
                {
                    return FailureResult();
                }
            }

            if ( d != null )
            {
                Dictionary<string, object> userData = d.ToKeyValuePairs();
                OSDMap doc = new OSDMap(9);
//
//                XmlDocument doc = new XmlDocument();
//
//                XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");
//
//                doc.AppendChild(xmlnode);
//
//                XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");
//
//                doc.AppendChild(rootElement);

                foreach (KeyValuePair<string, object> item in userData) {

                    doc[item.Key] = OSD.FromString(item.Value.ToString());
//
//                    XmlNode kvpair = doc.CreateElement("", item.Key,"");
//                    rootElement.AppendChild(kvpair);
//                    kvpair.AppendChild(doc.CreateTextNode(item.Value.ToString()));
                }
                return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(doc));
                // return doc.
            }
            return FailureResult();
        }

        private byte[] TestResponse (Dictionary<string, object> request)
        {

            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse", "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Response", "");
            result.AppendChild(doc.CreateTextNode("GoodBye!"));

            rootElement.AppendChild(result);

            if ( request.ContainsKey("HELLO"))
            {
                m_log.InfoFormat("[BWx]: Testing {0}", request["HELLO"].ToString());
                XmlElement response = doc.CreateElement("", "Greeting", "");
                rootElement.AppendChild(response);

                response.AppendChild(doc.CreateTextNode(request["HELLO"].ToString()));

                rootElement.AppendChild(response);

            }
            return DocToBytes(doc);
        }
    }
}

