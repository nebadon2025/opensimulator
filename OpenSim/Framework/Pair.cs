namespace OpenSim.Framework
{
    public class Pair<T1, T2>
    {
        public Pair(T1 t1, T2 t2)
        {
            First = t1;
            Second = t2;
        }

        public T1 First;
        public T2 Second;
    }
}
