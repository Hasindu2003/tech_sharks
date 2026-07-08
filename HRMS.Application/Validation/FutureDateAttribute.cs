using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class FutureDateAttribute : ValidationAttribute
    {
        public int MinimumDaysAhead { get; set; } = 1;
        public int MaxYearsAhead { get; set; } = 1;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            if (value is not DateTime dateValue)
                return new ValidationResult("Invalid date format.");

            var minDate = DateTime.Today.AddDays(MinimumDaysAhead);
            var maxDate = DateTime.Today.AddYears(MaxYearsAhead);

            if (dateValue.Date < minDate)
                return new ValidationResult(ErrorMessage ?? $"Date must be at least {MinimumDaysAhead} day(s) from today.");

            if (dateValue.Date > maxDate)
                return new ValidationResult(ErrorMessage ?? $"Date cannot be more than {MaxYearsAhead} year(s) from today.");

            return ValidationResult.Success;
        }
    }
}
