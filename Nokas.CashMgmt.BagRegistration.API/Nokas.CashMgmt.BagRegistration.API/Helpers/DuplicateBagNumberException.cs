namespace Nokas.CashMgmt.BagRegistration.API.Helpers
{
    public class DuplicateBagNumberException : Exception
    {
        public DuplicateBagNumberException(string bagNumber)
            : base($"Bag number '{bagNumber}' already exists.") { }
    }
}
