using DroidDeck.Software;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Xunit;

namespace DroidDeck.Tests
{
    public class SoftwareDataTests
    {
        [Fact]
        public void Validation_ShouldFail_WhenNameIsNull()
        {
            var data = new SoftwareData { Name = null };
            var context = new ValidationContext(data);
            var results = new List<ValidationResult>();

            // The class has [Required] on Name
            bool isValid = Validator.TryValidateObject(data, context, results, true);
            Assert.False(isValid);
        }

        [Fact]
        public void Validation_ShouldPass_WhenNameIsProvided()
        {
            var data = new SoftwareData { Name = "TestApp" };
            var context = new ValidationContext(data);
            var results = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(data, context, results, true);
            Assert.True(isValid);
        }
    }
}
