# Components — Reference

## Complete Standalone Component Template

```typescript
@Component({
    selector:        'app-product-card',
    standalone:      true,
    imports:         [NgOptimizedImage, RouterLink, CurrencyPipe, DatePipe],
    templateUrl:     './product-card.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    host: { class: 'product-card' }  // preferred over [:host] in styles
})
export class ProductCardComponent {
    // ✅ inject() over constructor injection
    private readonly router = inject(Router);

    // ✅ required input — compile error if parent doesn't provide it
    @Input({ required: true }) product!: Product;

    // ✅ Output with EventEmitter typed
    @Output() addedToCart = new EventEmitter<Product>();

    // ✅ Input alias — for external API compatibility
    @Input('productData') data?: Product;

    // ✅ Signal-based computed from input (Angular 17.1+ signal inputs)
    // @Input({ required: true }) product = input.required<Product>();
    // protected displayPrice = computed(() => formatCurrency(this.product().price));
}
```

---

## Lifecycle Hooks — Correct Usage

```typescript
export class ProductDetailComponent implements OnInit, OnDestroy {
    private readonly destroyRef = inject(DestroyRef);

    // ✅ Use reactive streams over imperative lifecycle when possible
    readonly product$ = inject(ActivatedRoute).paramMap.pipe(
        map(p => p.get('id')!),
        switchMap(id => inject(ProductService).getById(id)),
        takeUntilDestroyed()  // no destroyRef needed when called in constructor context
    );

    // ✅ ngOnInit — initial setup only, not data fetching
    ngOnInit(): void {
        // Analytics, DOM measurements, third-party lib init
    }

    // ✅ ngOnChanges — react to input changes
    ngOnChanges(changes: SimpleChanges): void {
        if (changes['product'] && !changes['product'].firstChange) {
            this.onProductChanged(changes['product'].currentValue);
        }
    }

    // ✅ ngOnDestroy — cleanup (prefer takeUntilDestroyed or DestroyRef)
    ngOnDestroy(): void {
        // Only needed for third-party lib cleanup or addEventListener removal
    }
}
```

### Lifecycle Hook Order
```
Constructor → ngOnChanges → ngOnInit → ngDoCheck →
ngAfterContentInit → ngAfterContentChecked →
ngAfterViewInit → ngAfterViewChecked → ngOnDestroy
```

**Rules:**
- Never do heavy work in `constructor` — use `ngOnInit`
- Avoid `ngDoCheck` and `ngAfterViewChecked` — run on every CD cycle
- Prefer reactive streams over `ngOnChanges` for input reactions

---

## Content Projection Patterns

```typescript
// Single slot
@Component({
    selector: 'app-card',
    template: `
        <div class="card">
            <div class="card-body">
                <ng-content/>
            </div>
        </div>
    `
})
export class CardComponent {}

// Multi-slot with select
@Component({
    selector:  'app-dialog',
    template: `
        <div class="dialog">
            <div class="dialog-header">
                <ng-content select="[dialogTitle]"/>
            </div>
            <div class="dialog-body">
                <ng-content/>
            </div>
            <div class="dialog-footer">
                <ng-content select="[dialogActions]"/>
            </div>
        </div>
    `
})
export class DialogComponent {}

// Usage
// <app-dialog>
//     <h2 dialogTitle>Confirm Delete</h2>
//     <p>Are you sure?</p>
//     <div dialogActions>
//         <button (click)="cancel()">Cancel</button>
//         <button (click)="confirm()">Delete</button>
//     </div>
// </app-dialog>
```

---

## ViewChild and ContentChild

```typescript
export class FormComponent implements AfterViewInit {
    // ✅ Use signal-based queries in Angular 17.1+ 
    // protected emailInput = viewChild.required<ElementRef>('emailInput');
    
    // Standard ViewChild
    @ViewChild('emailInput') emailInput!: ElementRef<HTMLInputElement>;
    @ViewChild(ChildComponent) child!: ChildComponent;

    ngAfterViewInit(): void {
        // ViewChild is available here — not in ngOnInit
        this.emailInput.nativeElement.focus();
    }
}

// ❌ Accessing ViewChild in ngOnInit — always undefined
ngOnInit(): void {
    this.emailInput.nativeElement.focus();  // undefined!
}
```
