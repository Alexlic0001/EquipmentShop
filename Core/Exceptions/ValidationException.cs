namespace EquipmentShop.Core.Exceptions
{
    public class ValidationException : Exception
    {
        public Dictionary<string, string[]> Errors { get; }

        //public ValidationException(Dictionary<string, string[]> errors)
        //    : base("Произошла одна или несколько ошибок валидации")
        //{
        //    Errors = errors;
        //}

        //public ValidationException(string propertyName, string errorMessage)
        //    : base(errorMessage)
        //{
        //    Errors = new Dictionary<string, string[]>
        //    {
        //        [propertyName] = new[] { errorMessage }
        //    };
        //}
    }
}
