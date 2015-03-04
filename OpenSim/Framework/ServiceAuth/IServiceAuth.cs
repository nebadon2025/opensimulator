using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

namespace OpenSim.Framework.ServiceAuth
{
    public delegate void AddHeaderDelegate(string key, string value);

    public interface  IServiceAuth
    {
        bool Authenticate(string data);
        bool Authenticate(NameValueCollection headers, AddHeaderDelegate d, out HttpStatusCode statusCode);
        void AddAuthorization(NameValueCollection headers);
    }
}
