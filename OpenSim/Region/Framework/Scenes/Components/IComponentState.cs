namespace OpenSim.Region.Framework.Scenes.Components
{
    public interface IComponentState
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data">A serializable type</param>
        void Set<T>(string name, T data);

        T Get<T>(string name);

        bool TryGet<T>(string name, out T data);

        string Serialise();
    }
}
