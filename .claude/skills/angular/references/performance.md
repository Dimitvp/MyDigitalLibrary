# Performance — Reference

## Change Detection Strategy

```typescript
// Default — Angular checks on every event, timer, HTTP response
// OnPush  — Angular checks only when:
//           1. An @Input() reference changes
//           2. An event originates from this component or its children
//           3. An async pipe emits a new value
//           4. markForCheck() is called explicitly

// ✅ All presentational components should use OnPush
@Component({
    changeDetection: ChangeDetectionStrategy.OnPush,
    // ...
})
export class ProductCardComponent {
    @Input({ required: true }) product!: Product;
}

// ✅ When you must trigger CD manually (third-party lib, manual DOM update)
export class ChartComponent {
    private readonly cdr = inject(ChangeDetectorRef);

    updateChart(data: ChartData): void {
        this.chartInstance.setData(data);  // third-party, outside Angular zone
        this.cdr.markForCheck();           // tells Angular to check this component
    }
}

// ✅ Run outside Angular zone for non-Angular events (improves perf)
export class CanvasComponent {
    private readonly ngZone = inject(NgZone);
    private readonly canvas = inject(ElementRef<HTMLCanvasElement>);

    startAnimation(): void {
        this.ngZone.runOutsideAngular(() => {
            // requestAnimationFrame doesn't trigger CD
            const animate = () => {
                this.drawFrame();
                requestAnimationFrame(animate);
            };
            requestAnimationFrame(animate);
        });
    }
}
```

---

## Virtual Scrolling — Large Lists

```typescript
// ✅ CDK Virtual Scroll for large lists (> 100 items)
// app.module / standalone imports: ScrollingModule

@Component({
    standalone: true,
    imports: [ScrollingModule],
    template: `
        <cdk-virtual-scroll-viewport itemSize="72" class="viewport">
            <app-product-card
                *cdkVirtualFor="let product of products; trackBy: trackById"
                [product]="product"
            />
        </cdk-virtual-scroll-viewport>
    `,
    styles: [`.viewport { height: 600px; }`]
})
export class ProductListComponent {
    @Input() products: Product[] = [];
    trackById = (_: number, p: Product) => p.id;
}
```

---

## Bundle Optimisation Checklist

```
✅ All feature routes lazy loaded (loadChildren / loadComponent)
✅ No barrel files (index.ts re-exporting everything) in large features
   → Barrel files prevent tree-shaking
✅ Import only what you need from RxJS operators
   import { map, filter } from 'rxjs/operators'  ✅
   import * as Rx from 'rxjs'                     ❌
✅ Images use NgOptimizedImage
✅ Heavy third-party libs (charts, editors) deferred with @defer
✅ providedIn: 'root' on all tree-shakeable services (not module providers)
```

---

## Defer Triggers Reference

```html
<!-- on idle — when browser is idle (default if no trigger) -->
@defer (on idle) { <app-heavy/> }

<!-- on viewport — when placeholder enters viewport -->
@defer (on viewport) { <app-chart/> }

<!-- on interaction — when user clicks/focuses placeholder -->
@defer (on interaction) { <app-comments/> }

<!-- on hover — when user hovers placeholder -->
@defer (on hover) { <app-tooltip-content/> }

<!-- on timer — after a delay -->
@defer (on timer(2s)) { <app-cookie-banner/> }

<!-- when — when a condition becomes true -->
@defer (when isLoggedIn) { <app-dashboard/> }

<!-- prefetch separately from render -->
@defer (on viewport; prefetch on idle) { <app-chart/> }
```

---

## trackBy / track Best Practices

```typescript
// ❌ No track — Angular destroys and recreates all DOM nodes on every update
@for (item of items; track $index) { ... }  // $index — ok for static lists only

// ✅ track by unique identity — Angular reuses existing DOM nodes
@for (item of items; track item.id) { ... }

// For *ngFor (Angular < 17)
@Component({ template: `<li *ngFor="let item of items; trackBy: trackById">` })
export class ListComponent {
    trackById = (_index: number, item: Item) => item.id;
    // ✅ Arrow function as property — stable reference, not recreated each CD cycle
    // ❌ trackById(index, item) { } — method binding creates new reference each time
}
```

---

## Signals vs Zone.js Performance

```typescript
// Signals work without Zone.js — enables zoneless Angular (experimental Angular 18)
// provideExperimentalZonelessChangeDetection()

// With signals, change detection is targeted — only affected components re-render
// vs Zone.js which triggers CD for the entire component tree on any async event

// ✅ Incremental migration: mix signals and Zone.js freely
// Signals automatically call markForCheck() when they change in a component
export class ProductComponent {
    // Changing this signal automatically marks the component for check
    private readonly cartCount = inject(CartService).itemCount;
    // Template: {{ cartCount() }} — updates only when signal changes
}
```
