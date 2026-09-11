# Forms — Reference

## Typed Reactive Forms (Angular 14+)

```typescript
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

// Define form type interface
interface RegisterForm {
    email:           FormControl<string>;
    password:        FormControl<string>;
    confirmPassword: FormControl<string>;
    profile: FormGroup<{
        firstName: FormControl<string>;
        lastName:  FormControl<string>;
    }>;
}

@Component({ standalone: true, imports: [ReactiveFormsModule] })
export class RegisterComponent {
    private readonly fb = inject(FormBuilder);

    form = this.fb.group<RegisterForm>({
        email:    this.fb.nonNullable.control('', [Validators.required, Validators.email]),
        password: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(8)]),
        confirmPassword: this.fb.nonNullable.control('', Validators.required),
        profile:  this.fb.group({
            firstName: this.fb.nonNullable.control('', Validators.required),
            lastName:  this.fb.nonNullable.control('', Validators.required)
        })
    }, { validators: passwordMatchValidator });

    // ✅ getRawValue() includes disabled controls — form.value does not
    onSubmit(): void {
        if (this.form.invalid) return;
        const data = this.form.getRawValue();  // fully typed
    }
}
```

---

## Custom Validators

```typescript
// Sync validator
export const passwordMatchValidator: ValidatorFn = (group: AbstractControl) => {
    const password        = group.get('password')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return password === confirmPassword ? null : { passwordMismatch: true };
};

// Async validator — DB uniqueness check
export function uniqueEmailValidator(authService: AuthService): AsyncValidatorFn {
    return (control: AbstractControl): Observable<ValidationErrors | null> =>
        control.valueChanges.pipe(
            debounceTime(400),
            take(1),
            switchMap(email => authService.checkEmailAvailable(email)),
            map(available => available ? null : { emailTaken: true }),
            catchError(() => of(null))
        );
}

// Usage
email: this.fb.nonNullable.control('',
    { validators: [Validators.required, Validators.email],
      asyncValidators: uniqueEmailValidator(inject(AuthService)) }
)

// Shared validators — declared once, used everywhere
export class SharedValidators {
    static strongPassword(): ValidatorFn {
        return (ctrl: AbstractControl): ValidationErrors | null => {
            const v = ctrl.value as string;
            if (!v) return null;
            const errors: ValidationErrors = {};
            if (v.length < 8)         errors['tooShort']    = true;
            if (!/[A-Z]/.test(v))     errors['noUppercase'] = true;
            if (!/[0-9]/.test(v))     errors['noNumber']    = true;
            if (!/[^a-zA-Z0-9]/.test(v)) errors['noSpecial'] = true;
            return Object.keys(errors).length ? errors : null;
        };
    }
}
```

---

## Error Display Pattern

```typescript
// Error display component — reuse across forms
@Component({
    selector:   'app-field-error',
    standalone: true,
    imports:    [NgIf],
    template: `
        @if (control?.invalid && (control?.dirty || control?.touched)) {
            @if (control?.errors?.['required'])     { <span>This field is required.</span> }
            @if (control?.errors?.['email'])        { <span>Enter a valid email address.</span> }
            @if (control?.errors?.['minlength'])    { <span>Minimum {{ control?.errors?.['minlength'].requiredLength }} characters.</span> }
            @if (control?.errors?.['emailTaken'])   { <span>This email is already registered.</span> }
            @if (control?.errors?.['serverError'])  { <span>{{ control?.errors?.['serverError'] }}</span> }
        }
    `
})
export class FieldErrorComponent {
    @Input() control?: AbstractControl | null;
}

// ✅ Map server 422 errors back to form fields
handleServerErrors(err: HttpErrorResponse): void {
    if (err.status === 422 && err.error?.validationDetails) {
        Object.entries(err.error.validationDetails).forEach(([field, messages]) => {
            this.form.get(field)?.setErrors({ serverError: (messages as string[])[0] });
            this.form.get(field)?.markAsTouched();
        });
    }
}
```

---

## FormArray Pattern

```typescript
interface OrderForm {
    customerId: FormControl<string>;
    lines: FormArray<FormGroup<OrderLineForm>>;
}
interface OrderLineForm {
    productId: FormControl<string>;
    quantity:  FormControl<number>;
    unitPrice: FormControl<number>;
}

@Component({ standalone: true })
export class OrderFormComponent {
    private readonly fb = inject(FormBuilder);

    form = this.fb.group<OrderForm>({
        customerId: this.fb.nonNullable.control('', Validators.required),
        lines:      this.fb.array<FormGroup<OrderLineForm>>([])
    });

    get lines() { return this.form.controls.lines; }

    addLine(): void {
        this.lines.push(this.fb.group<OrderLineForm>({
            productId: this.fb.nonNullable.control('', Validators.required),
            quantity:  this.fb.nonNullable.control(1, [Validators.required, Validators.min(1)]),
            unitPrice: this.fb.nonNullable.control(0, Validators.required)
        }));
    }

    removeLine(index: number): void {
        this.lines.removeAt(index);
    }

    readonly lineTotal = (index: number) => computed(() => {
        const line = this.lines.at(index);
        return line.value.quantity! * line.value.unitPrice!;
    });
}
```
