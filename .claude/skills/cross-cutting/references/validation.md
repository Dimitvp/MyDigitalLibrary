# Validation Pipelines — Reference

## FluentValidation + MediatR Pipeline Behaviour

```csharp
// ValidationBehaviour.cs
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context  = new ValidationContext<TRequest>(request);
        var results  = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);  // → GlobalExceptionHandler → 422

        return await next();
    }
}

// Program.cs
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));    // log before validate
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>));      // audit after success
});
```

## Shared Validation Rules — AbstractValidator Base Classes

```csharp
// EmailValidator.cs — reusable rule, declared once
public static class SharedRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

    public static IRuleBuilderOptions<T, string> MustBeValidPhoneNumber<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Phone number is not valid.");
}

// Usage in any validator — no duplication
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).MustBeValidEmail();
        RuleFor(x => x.Phone).MustBeValidPhoneNumber();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Email).MustBeValidEmail(); // same rule, zero duplication
    }
}
```

## Async Validation — DB Uniqueness Checks

```csharp
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(IUserRepository repo)
    {
        RuleFor(x => x.Email)
            .MustBeValidEmail()
            .MustAsync(async (email, ct) => !await repo.ExistsWithEmailAsync(email, ct))
            .WithMessage("A user with this email already exists.");
    }
}
```

## Angular — Shared Reactive Form Validators

```typescript
// validators/shared.validators.ts — declared once, used everywhere
export class SharedValidators {
    static email(): ValidatorFn {
        return Validators.compose([
            Validators.required,
            Validators.email,
            Validators.maxLength(256)
        ])!;
    }

    static strongPassword(): ValidatorFn {
        return (control: AbstractControl): ValidationErrors | null => {
            const v = control.value as string;
            if (!v) return null;
            const errors: ValidationErrors = {};
            if (v.length < 8)            errors['minLength'] = true;
            if (!/[A-Z]/.test(v))        errors['uppercase'] = true;
            if (!/[0-9]/.test(v))        errors['number']    = true;
            return Object.keys(errors).length ? errors : null;
        };
    }
}

// Usage in any form — consistent, no duplication
this.form = this.fb.group({
    email:    ['', SharedValidators.email()],
    password: ['', SharedValidators.strongPassword()]
});

// api-validation.service.ts — maps 422 errors back to form fields
@Injectable({ providedIn: 'root' })
export class ApiValidationService {
    applyErrors(form: FormGroup, errors: ValidationError[]): void {
        errors.forEach(err => {
            const ctrl = form.get(err.propertyName.toLowerCase());
            ctrl?.setErrors({ serverError: err.errorMessage });
        });
    }
}
```
