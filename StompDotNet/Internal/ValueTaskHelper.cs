using System.Threading.Tasks;

namespace StompDotNet.Internal
{

    public static class ValueTaskHelper
    {

        public static ValueTask CompletedTask
        {
            get
            {
#if NETSTANDARD2_2
                return ValueTask.CompletedTask;
#else
                return new ValueTask();
#endif
            }
        }

    }

}
