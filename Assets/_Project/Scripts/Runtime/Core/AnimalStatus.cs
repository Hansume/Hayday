namespace FarmGame.Core
{
    public enum AnimalStatus
    {
        /// <summary>Waiting for 1 pack of feed.</summary>
        Hungry,

        /// <summary>Fed; its product isn't ready yet.</summary>
        Producing,

        /// <summary>Its product is ready to collect.</summary>
        Ready
    }
}
