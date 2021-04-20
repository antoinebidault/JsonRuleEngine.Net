namespace JsonRuleEngine.Net
{
    /// <summary>
    /// The expression validation result
    /// </summary>
    public class ValidateExpressionResult
    {
        public string InvalidField { get; set; }
        public bool Success { get; set; }

        public static ValidateExpressionResult Valid = new ValidateExpressionResult()
        {
            Success = true
        };
    }

}