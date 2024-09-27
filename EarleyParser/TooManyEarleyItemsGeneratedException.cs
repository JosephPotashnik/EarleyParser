using System;

namespace EarleyParser
{
    /// <summary>
    /// This Exception is called when the Parser exceeds too many completed Items in a Set (e.g, 10000+)
    /// </summary>
    [Serializable]
    public class TooManyEarleyItemsGeneratedException : Exception
    {
        public TooManyEarleyItemsGeneratedException() { }
        public TooManyEarleyItemsGeneratedException(string message) : base(message) { }
        public TooManyEarleyItemsGeneratedException(string message, Exception innerException) : base(message, innerException) { }
    }
}