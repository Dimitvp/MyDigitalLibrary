# SOLID Examples — .NET & Angular

## S — Single Responsibility

### ❌ Violation (C#)
```csharp
public class UserService
{
    public void RegisterUser(User user)
    {
        // Validates
        if (string.IsNullOrEmpty(user.Email)) throw new Exception("Invalid email");

        // Saves to DB
        _db.Users.Add(user);
        _db.SaveChanges();

        // Sends welcome email
        var smtp = new SmtpClient("smtp.gmail.com");
        smtp.Send("welcome@app.com", user.Email, "Welcome!", "Hello " + user.Name);

        // Logs
        File.AppendAllText("log.txt", $"User {user.Email} registered at {DateTime.Now}");
    }
}
```

### ✅ Fixed (C#)
```csharp
public class UserRegistrationService
{
    private readonly IUserRepository _repo;
    private readonly IEmailService _email;
    private readonly ILogger<UserRegistrationService> _logger;

    public UserRegistrationService(IUserRepository repo, IEmailService email, ILogger<UserRegistrationService> logger)
    {
        _repo = repo; _email = email; _logger = logger;
    }

    public async Task RegisterAsync(User user)
    {
        UserValidator.Validate(user);           // validation concern
        await _repo.AddAsync(user);             // persistence concern
        await _email.SendWelcomeAsync(user);    // notification concern
        _logger.LogInformation("User {Email} registered", user.Email); // logging concern
    }
}
```

---

## O — Open/Closed

### ❌ Violation (C#)
```csharp
public class DiscountCalculator
{
    public decimal Calculate(Order order)
    {
        if (order.CustomerType == "VIP")
            return order.Total * 0.2m;
        else if (order.CustomerType == "Member")
            return order.Total * 0.1m;
        else if (order.CustomerType == "Employee")
            return order.Total * 0.3m;
        return 0;
    }
}
```

### ✅ Fixed (C#)
```csharp
public interface IDiscountStrategy
{
    decimal Calculate(Order order);
}

public class VipDiscount : IDiscountStrategy
{
    public decimal Calculate(Order order) => order.Total * 0.2m;
}

public class MemberDiscount : IDiscountStrategy
{
    public decimal Calculate(Order order) => order.Total * 0.1m;
}

// Register in DI: services.AddKeyedScoped<IDiscountStrategy, VipDiscount>("VIP");
// New discount types = new class, zero changes to existing code
```

---

## L — Liskov Substitution

### ❌ Violation (C#)
```csharp
public abstract class Shape { public abstract double Area(); }

public class Rectangle : Shape
{
    public virtual double Width { get; set; }
    public virtual double Height { get; set; }
    public override double Area() => Width * Height;
}

public class Square : Rectangle
{
    public override double Width { set { base.Width = value; base.Height = value; } }
    public override double Height { set { base.Width = value; base.Height = value; } }
    // Breaks Rectangle contract — setting Width silently changes Height
}
```

### ✅ Fixed (C#)
```csharp
public abstract class Shape { public abstract double Area(); }
public class Rectangle : Shape
{
    public double Width { get; init; }
    public double Height { get; init; }
    public override double Area() => Width * Height;
}
public class Square : Shape
{
    public double Side { get; init; }
    public override double Area() => Side * Side;
}
```

---

## I — Interface Segregation

### ❌ Violation (C#)
```csharp
public interface IRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
    void Add(T entity);
    void Update(T entity);
    void Delete(int id);
    IEnumerable<T> Search(string query);      // not needed by all consumers
    void BulkInsert(IEnumerable<T> entities); // not needed by all consumers
    void Archive(int id);                      // not needed by all consumers
}
```

### ✅ Fixed (C#)
```csharp
public interface IReadRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

public interface IWriteRepository<T>
{
    void Add(T entity);
    void Update(T entity);
    void Delete(int id);
}

public interface ISearchRepository<T>
{
    IEnumerable<T> Search(string query);
}

// Consumers depend only on what they need:
// QueryHandler : IReadRepository<Product>
// CommandHandler : IWriteRepository<Product>
```

---

## D — Dependency Inversion

### ❌ Violation (Angular)
```typescript
@Component({ selector: 'app-users', ... })
export class UsersComponent implements OnInit {
    users: User[] = [];

    ngOnInit() {
        // Direct HttpClient usage — tightly coupled to HTTP infrastructure
        const http = new HttpClient(...); // also: never do `new` for Angular services
        http.get<User[]>('https://api.example.com/users').subscribe(u => this.users = u);
    }
}
```

### ✅ Fixed (Angular)
```typescript
// user.service.ts
@Injectable({ providedIn: 'root' })
export class UserService {
    constructor(private http: HttpClient, private env: EnvironmentService) {}

    getUsers(): Observable<User[]> {
        return this.http.get<User[]>(`${this.env.apiUrl}/users`);
    }
}

// users.component.ts
@Component({ selector: 'app-users', ... })
export class UsersComponent implements OnInit {
    users$: Observable<User[]>;

    constructor(private userService: UserService) {}  // depend on abstraction

    ngOnInit() {
        this.users$ = this.userService.getUsers();
    }
}
```
