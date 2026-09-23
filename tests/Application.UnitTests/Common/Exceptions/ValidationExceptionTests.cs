using BillingSaaS.Application.Common.Exceptions;
using FluentValidation.Results;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Common.Exceptions;

public class ValidationExceptionTests
{
    [Test]
    public void DefaultConstructorCreatesAnEmptyErrorDictionary()
    {
        var actual = new ValidationException().Errors;

        actual.Keys.ShouldBeEmpty();
    }

    [Test]
    public void SingleValidationFailureCreatesASingleElementErrorDictionary()
    {
        var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Age", "must be over 18"),
            };

        var actual = new ValidationException(failures).Errors;

        actual.Keys.ShouldBe(new string[] { "Age" });
        actual["Age"].ShouldBe(new string[] { "must be over 18" });
    }

    [Test]
    public void MulitpleValidationFailureForMultiplePropertiesCreatesAMultipleElementErrorDictionaryEachWithMultipleValues()
    {
        var failures = new List<ValidationFailure>
            {
                new ValidationFailure("Age", "must be 18 or older"),
                new ValidationFailure("Age", "must be 25 or younger"),
                new ValidationFailure("Password", "must contain at least 8 characters"),
                new ValidationFailure("Password", "must contain a digit"),
                new ValidationFailure("Password", "must contain upper case letter"),
                new ValidationFailure("Password", "must contain lower case letter"),
            };

        var actual = new ValidationException(failures).Errors;

        actual.Keys.ShouldBe(new string[] { "Password", "Age" }, ignoreOrder: true);

        actual["Age"].ShouldBe(new string[]
        {
                "must be 25 or younger",
                "must be 18 or older",
        }, ignoreOrder: true);

        actual["Password"].ShouldBe(new string[]
        {
                "must contain lower case letter",
                "must contain upper case letter",
                "must contain at least 8 characters",
                "must contain a digit",
        }, ignoreOrder: true);
    }

    [Test]
    public void ValidationExceptionMessageContainsFormattedPropertyErrors()
    {
        var failures = new List<ValidationFailure>
        {
            new ValidationFailure("Identificacion", "El número de identificación es requerido"),
            new ValidationFailure("Email", "El email no tiene un formato válido")
        };

        var exception = new ValidationException(failures);

        exception.Message.ShouldContain("Identificacion");
        exception.Message.ShouldContain("El número de identificación es requerido");
        exception.Message.ShouldContain("Email");
        exception.Message.ShouldContain("El email no tiene un formato válido");
    }
}
