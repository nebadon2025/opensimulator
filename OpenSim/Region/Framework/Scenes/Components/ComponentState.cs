using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OpenSim.Region.Framework.Scenes.Components
{
    [Serializable]
    public class ComponentState
    {
        private readonly Dictionary<string,Object> m_stateData = new Dictionary<string,object>();

        public void Set<T>(string name, T data)
        {
            if (typeof(T).IsSerializable)
            {
                m_stateData[name] = data;
            }
            else
            {
                throw new SerializationException("Unable to set " + name + " as value because " + typeof (T) +
                                                 " is not a serializable type.");
            }
        }

        public bool TryGet<T>(string name, out T val)
        {
            Object x;
            if(m_stateData.TryGetValue(name, out x))
            {
                if(x is T)
                {
                    val = (T)x;
                    return true;
                }
            }
            val = default(T);
            return false;
        }

        public T Get<T>(string name)
        {
            return (T) m_stateData[name];
        }

        public string Serialise()
        {
            throw new NotImplementedException();
        }
    }
}
