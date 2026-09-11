# Angular Testing Patterns — Reference

## Component Unit Test

```typescript
describe('ProductCardComponent', () => {
    let component: ProductCardComponent;
    let fixture:   ComponentFixture<ProductCardComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [ProductCardComponent],  // standalone component
        }).compileComponents();

        fixture   = TestBed.createComponent(ProductCardComponent);
        component = fixture.componentInstance;

        // Set required inputs
        fixture.componentRef.setInput('product', {
            id: '1', name: 'Widget', price: 9.99, currency: 'EUR'
        });
        fixture.detectChanges();
    });

    it('should display the product name', () => {
        const nameEl = fixture.debugElement.query(By.css('[data-testid="product-name"]'));
        expect(nameEl.nativeElement.textContent).toContain('Widget');
    });

    it('should emit selected event when add to cart clicked', () => {
        const emitted: Product[] = [];
        component.addedToCart.subscribe(p => emitted.push(p));

        fixture.debugElement.query(By.css('[data-testid="add-to-cart"]'))
            .nativeElement.click();

        expect(emitted).toHaveSize(1);
        expect(emitted[0].id).toBe('1');
    });
});
```

---

## Service Test with HTTP

```typescript
describe('ProductService', () => {
    let service:    ProductService;
    let httpMock:   HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                ProductService,
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: API_BASE_URL, useValue: 'https://api.test.com' }
            ]
        });

        service  = TestBed.inject(ProductService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());  // ensure no unexpected requests

    it('getAll should return products from API', () => {
        const mockProducts: Product[] = [
            { id: '1', name: 'Widget', price: 9.99, currency: 'EUR' }
        ];

        let result: Product[] | undefined;
        service.getAll().subscribe(p => result = p);

        const req = httpMock.expectOne('https://api.test.com/products');
        expect(req.request.method).toBe('GET');
        req.flush(mockProducts);

        expect(result).toEqual(mockProducts);
    });

    it('getAll should return empty array on HTTP error', () => {
        let result: Product[] | undefined;
        service.getAll().subscribe(p => result = p);

        httpMock.expectOne('https://api.test.com/products')
            .flush('Server error', { status: 500, statusText: 'Internal Server Error' });

        expect(result).toEqual([]);
    });
});
```

---

## Signal-Based Component Test

```typescript
describe('CartComponent (signals)', () => {
    let fixture:   ComponentFixture<CartComponent>;
    let cartState: CartStateService;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports:   [CartComponent],
            providers: [CartStateService]
        }).compileComponents();

        fixture   = TestBed.createComponent(CartComponent);
        cartState = TestBed.inject(CartStateService);
        fixture.detectChanges();
    });

    it('should display item count from signal', () => {
        cartState.addItem({ id: '1', name: 'Widget', price: 10, qty: 1 });
        fixture.detectChanges();

        const countEl = fixture.debugElement.query(By.css('[data-testid="item-count"]'));
        expect(countEl.nativeElement.textContent).toContain('1');
    });
});
```

---

## Async Testing Patterns

```typescript
// fakeAsync + tick — control time in tests
it('should debounce search input', fakeAsync(() => {
    component.searchControl.setValue('widget');
    tick(300);  // advance time 300ms
    fixture.detectChanges();

    expect(mockSearchService.search).toHaveBeenCalledWith('widget');
}));

// waitForAsync — for real promises
it('should load products on init', waitForAsync(() => {
    fixture.whenStable().then(() => {
        expect(component.products().length).toBeGreaterThan(0);
    });
}));
```
