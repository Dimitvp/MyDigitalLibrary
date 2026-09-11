# TypeScript / Angular OOP Patterns — Reference

## Access Modifiers in TypeScript

```typescript
// TypeScript access modifiers — compile-time only (erased at runtime)
class OrderService {
    private readonly _repo: OrderRepository;      // private — class only
    protected readonly logger: LoggerService;      // protected — class + subclasses
    public readonly orderId: string;               // public — anywhere (default)
    readonly #secret: string;                      // private field — true JS private (runtime)

    constructor(repo: OrderRepository, logger: LoggerService) {
        this._repo  = repo;    // longhand
        this.logger = logger;
    }
}

// Shorthand constructor injection (preferred in Angular services/components)
class ProductService {
    constructor(
        private readonly http: HttpClient,          // private readonly
        private readonly cache: CacheService,       // private readonly
        protected readonly logger: LoggerService    // protected readonly
    ) {}
}
```

---

## Generics in TypeScript OOP

```typescript
// Generic class with constraint
export abstract class BaseRepository<T extends { id: number }> {
    protected abstract getAll(): Observable<T[]>;
    protected abstract getById(id: number): Observable<T>;

    findById(id: number): Observable<T | undefined> {
        return this.getAll().pipe(
            map(items => items.find(item => item.id === id))
        );
    }
}

// Generic interface
export interface Repository<T, TId = number> {
    getById(id: TId): Observable<T | null>;
    getAll(): Observable<T[]>;
    create(item: Omit<T, 'id'>): Observable<T>;
    update(id: TId, item: Partial<T>): Observable<T>;
    delete(id: TId): Observable<void>;
}
```

---

## TypeScript Interfaces vs Abstract Classes

```typescript
// interface — contract only, no implementation, multiple allowed
interface Serializable {
    serialize(): string;
    deserialize(data: string): void;
}

interface Loggable {
    log(message: string): void;
}

// A class can implement multiple interfaces
class AuditService implements Serializable, Loggable { ... }

// abstract class — can share implementation, but single inheritance
abstract class BaseFormComponent {
    protected abstract buildForm(): FormGroup;  // must override

    protected validate(): boolean {             // shared implementation
        return this.form.valid;
    }

    form!: FormGroup;
    ngOnInit(): void { this.form = this.buildForm(); }
}

// Decision:
// - Multiple behaviours needed on one class? → interfaces
// - Shared code between related classes?     → abstract class
// - Can't decide?                            → interface (safer default)
```

---

## Readonly and Immutability Patterns

```typescript
// readonly property — set once, then immutable
class Config {
    readonly apiUrl: string;
    readonly timeout: number;

    constructor(apiUrl: string, timeout = 5000) {
        this.apiUrl  = apiUrl;
        this.timeout = timeout;
    }
}

// Readonly<T> utility type — makes all properties readonly
type ReadonlyConfig = Readonly<Config>;

// Object.freeze() — runtime immutability (shallow)
const DEFAULTS = Object.freeze({ retries: 3, timeout: 5000 });

// Immutable array patterns in Angular
@Injectable({ providedIn: 'root' })
export class CartService {
    private readonly _items$ = new BehaviorSubject<readonly CartItem[]>([]);
    readonly items$ = this._items$.asObservable();

    addItem(item: CartItem): void {
        // Spread creates new array — never mutate existing
        this._items$.next([...this._items$.getValue(), item]);
    }

    removeItem(id: string): void {
        this._items$.next(this._items$.getValue().filter(i => i.id !== id));
    }
}
```

---

## Decorators as OOP Metadata

```typescript
// Class decorators — Angular uses these extensively
@Injectable({ providedIn: 'root' })  // marks class as DI participant
@Component({ selector: 'app-root', template: '...' })  // marks class as component

// Method decorators — for validation, logging, caching
function Log(target: any, key: string, descriptor: PropertyDescriptor) {
    const original = descriptor.value;
    descriptor.value = function(...args: any[]) {
        console.log(`Calling ${key} with`, args);
        const result = original.apply(this, args);
        console.log(`${key} returned`, result);
        return result;
    };
    return descriptor;
}

class OrderService {
    @Log
    createOrder(request: CreateOrderRequest): Order { ... }
}
```

---

## Mixins — TypeScript Alternative to Multiple Inheritance

```typescript
// Mixin function pattern
type Constructor<T = {}> = new (...args: any[]) => T;

function Timestamped<TBase extends Constructor>(Base: TBase) {
    return class extends Base {
        readonly createdAt = new Date();
        updatedAt = new Date();

        touch() { this.updatedAt = new Date(); }
    };
}

function Activatable<TBase extends Constructor>(Base: TBase) {
    return class extends Base {
        isActive = false;
        activate()   { this.isActive = true;  }
        deactivate()  { this.isActive = false; }
    };
}

// Apply multiple mixins
class User { constructor(public name: string) {} }
const TimestampedActivatableUser = Activatable(Timestamped(User));

const user = new TimestampedActivatableUser('Alice');
user.activate();
console.log(user.createdAt, user.isActive);
```

---

## Angular Component OOP Patterns

### Smart / Dumb (Container / Presentational) Components
```typescript
// ✅ Smart component — handles data concerns, delegates display
@Component({
    selector: 'app-orders-page',
    template: `
        <app-orders-list
            [orders]="orders$ | async"
            [isLoading]="isLoading$ | async"
            (orderSelected)="onOrderSelected($event)"
        />
    `
})
export class OrdersPageComponent {
    readonly orders$    = this.orderService.getAll();
    readonly isLoading$ = this.orderService.isLoading$;

    constructor(private readonly orderService: OrderService) {}

    onOrderSelected(orderId: string): void {
        this.router.navigate(['/orders', orderId]);
    }
}

// ✅ Dumb component — pure display, no service dependencies
@Component({
    selector:   'app-orders-list',
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `...`
})
export class OrdersListComponent {
    @Input()  orders!:    Order[] | null;
    @Input()  isLoading!: boolean | null;
    @Output() orderSelected = new EventEmitter<string>();
}
```

### OnPush Change Detection — OOP Design Signal
```typescript
// ChangeDetectionStrategy.OnPush is compatible with immutable data only
// It's a signal that the component is designed with proper encapsulation:
// - inputs are immutable references
// - no mutable shared state accessed directly
@Component({
    changeDetection: ChangeDetectionStrategy.OnPush,
    // ...
})
// If you can't use OnPush → the component probably has encapsulation issues
```
