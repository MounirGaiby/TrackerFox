![image](https://github.com/user-attachments/assets/87c7f968-19c9-4347-bdb7-82b9343c1112)# Personal Finance Tracker "TrackFox"

A comprehensive personal finance management web application built with ASP.NET Core and Tailwind CSS. This application helps users manage their finances, track spending, analyze financial data, monitor market trends, and get AI-powered financial insights.

## Features

### Core Financial Management
- **Dashboard**: Overview of financial status, recent transactions, and account balances.
  ![image](https://github.com/user-attachments/assets/2927f7a5-24c6-4e41-8a1e-bf919b608fcc)

- **Accounts**: Manage multiple bank and investment accounts (e.g., savings, checking, investment portfolios).
  ![image](https://github.com/user-attachments/assets/7e929099-30b5-48a5-bb3b-463154b44438)

- **Transactions**: Record, categorize, and manage income and expenses with detailed information.
  ![image](https://github.com/user-attachments/assets/34eb1f2c-bb4f-4ad2-bfa5-c132c6673683)

- **Analytics**: Visualize spending patterns, income trends, and investment performance with interactive charts and reports.
  ![image](https://github.com/user-attachments/assets/d1f03883-cc0b-4202-abf3-91e91fcd5fdd)

- **Market**: Access real-time updates on cryptocurrency and stock values, along with breaking financial news.
  ![image](https://github.com/user-attachments/assets/8f359d06-0a6c-42f2-a10d-9de67d750440)

- **FinBot**: Your dedicated finance chatbot, providing personalized financial advice and 24/7 support.
  ![image](https://github.com/user-attachments/assets/7757c2bd-9337-432e-9ec9-ce25db25b334)


### Analytics
- **Financial Insights**: Visual breakdown of spending by category using interactive charts.
- **Spending Trends**: Track spending patterns over time to understand financial habits.
- **Account Analysis**: Detailed view of individual account activity and balance history.
- **Predictions**: AI-powered predictions of future expenses based on historical data (Note: This feature's scope and accuracy may vary).

### Markets
- **Market Overview**: Real-time data on stock indices, cryptocurrencies, forex, and commodities.
- **Stock Details**: Comprehensive stock information including current prices, historical data, and interactive charts.
- **Cryptocurrency Details**: Detailed cryptocurrency data, current prices, market cap, and price history charts.
- **Forex Details**: Currency pair exchange rates and historical trends for foreign exchange markets.
- **Commodity Details**: Information on various commodity prices, details, and historical data.

### AI Financial Assistant
- **Chat Interface**: Engage with a conversational AI assistant for financial questions and guidance.
- **Personalized Insights**: The AI can provide insights based on the data you input into the application.
- **Suggested Questions**: Receive dynamic follow-up suggestions based on the conversation context to explore topics further.
- **OpenRouter Integration**: Powered by models like Claude 3 Haiku via OpenRouter for intelligent financial guidance. (Note: Conversations are not stored or saved by the application).

## Technical Implementation

### Backend
- **ASP.NET Core MVC**: The web application is built using the Model-View-Controller pattern with ASP.NET Core, providing a robust and scalable foundation.
- **Entity Framework Core**: Used as the Object-Relational Mapper (ORM) for database interactions, simplifying data access and management.
- **PostgreSQL**: The primary database for storing financial data, user information, and application settings.
- **Identity Framework**: Manages user authentication (registration, login) and authorization, ensuring secure access to personal financial data.
- **Serilog**: Integrated for structured logging, providing detailed logs for debugging and monitoring application health.

### Frontend
- **Tailwind CSS**: A utility-first CSS framework used for rapidly building custom user interfaces with a modern look and feel.
- **Chart.js**: A JavaScript library for creating interactive and visually appealing charts to display financial data and analytics.
- **Responsive Design**: The application is designed to be mobile-friendly and adapt to various screen sizes for a consistent user experience across devices.

### APIs and Integration
- **OpenRouter API**: Integrated for the AI Financial Assistant, leveraging large language models for conversational AI capabilities.
- **Financial Market Data APIs**:
    - **AlphaVantage**: Used for fetching stock market data.
    - **CoinGecko**: Used for fetching cryptocurrency data.
    - **OpenWeather**: Potentially used for features requiring weather data (e.g., correlating spending with weather, though specific use might vary).
    (Note: API keys for these services need to be configured in `appsettings.json`).

## How the App Works

This application is built on the ASP.NET Core framework, leveraging several key technologies and patterns:

### 1. ASP.NET Core MVC
ASP.NET Core MVC is a rich framework for building web apps and APIs using the Model-View-Controller design pattern:
- **Models**: Represent the data of the application and business logic. Located in the `Models/` directory (e.g., `Account.cs`, `Transaction.cs`). `ViewModels/` are specifically crafted classes to pass data to and from Views.
- **Views**: Responsible for rendering the user interface. These are typically Razor files (`.cshtml`) located in the `Views/` directory, organized by controller.
- **Controllers**: Handle incoming browser requests, retrieve data from models (often via services), and select a view to return to the browser. Located in the `Controllers/` directory (e.g., `HomeController.cs`, `TransactionsController.cs`).

**Request Lifecycle (Simplified):**
1. An HTTP request arrives at the server.
2. ASP.NET Core routing directs the request to a specific action method in a controller.
3. The controller action processes the request. This may involve:
    - Interacting with services (in `Services/`) or directly with the database context (`ApplicationDbContext`).
    - Performing business logic.
    - Preparing data (often using ViewModels).
4. The controller action returns a result, typically a View, which then renders HTML to be sent back to the client's browser.

### 2. `appsettings.json`
This JSON file is used to store configuration data for the application.
- **Purpose**: Holds settings like database connection strings, logging levels, API keys, and other application parameters.
- **Structure**:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Database=personal_finance_tracker;Username=your_user;Password=your_password"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "OpenRouter": { // Example for AI service
      "ApiKey": "your_openrouter_api_key_here"
    },
    "MarketAPIs": { // Example for market data APIs
        "CoinGecko": {
            "ApiKey": "your_coingecko_api_key_here" 
        },
        "AlphaVantage": {
            "ApiKey": "your_alphavantage_api_key_here"
        }
        // Add other API keys as needed
    }
  }
  ```
- **Environment-Specific Configuration**: `appsettings.Development.json` or `appsettings.Production.json` can override settings for different environments.

### 3. Routing
Routing is responsible for matching incoming HTTP request URLs to controller actions.
- **Configuration**: Defined in `Program.cs` using `app.MapControllerRoute()`.
- **Default Route**: A common pattern is `pattern: "{controller=Home}/{action=Index}/{id?}"`.
    - `controller=Home`: If no controller is specified in the URL, it defaults to `HomeController`.
    - `action=Index`: If no action is specified, it defaults to the `Index` action method.
    - `id?`: `id` is an optional route parameter.
- **Example**: A request to `/Transactions/Details/5` would typically route to the `Details` action on the `TransactionsController`, passing `5` as the `id` parameter.

### 4. Entity Framework Core (EF Core)
EF Core is an open-source, cross-platform Object-Relational Mapper (ORM) for .NET.
- **`ApplicationDbContext.cs`**: Located in `Data/`, this class is the heart of EF Core usage.
    - It inherits from `IdentityDbContext<User>` (to integrate with ASP.NET Core Identity) or `DbContext`.
    - It contains `DbSet<TEntity>` properties for each entity in your model (e.g., `public DbSet<Account> Accounts { get; set; }`). These represent tables in the database.
- **Database Provider**: The application is configured to use PostgreSQL, as seen in `Program.cs` (`options.UseNpgsql(...)`) and the `Npgsql.EntityFrameworkCore.PostgreSQL` NuGet package.
- **`OnModelCreating(ModelBuilder builder)`**: This method in `ApplicationDbContext` is used to configure the model:
    - Defining relationships between entities (e.g., one-to-many, many-to-many).
    - Setting constraints (e.g., precision for decimal properties like `Balance` and `Amount`).
    - Seeding initial data (e.g., default categories).
- **Migrations**: EF Core Migrations allow you to evolve the database schema as your model changes over time. Commands like `dotnet ef migrations add InitialCreate` and `dotnet ef database update` are used to manage these. Migration files are stored in the `Migrations/` directory.

### 5. ASP.NET Core Identity
Provides services for user authentication and authorization.
- **Integration**: Works seamlessly with EF Core. `ApplicationDbContext` inherits from `IdentityDbContext<User>`, which provides `DbSet`s for Identity tables (Users, Roles, Claims, etc.).
- **Configuration**: In `Program.cs`, services for Identity are added and configured (`builder.Services.AddIdentity<User, IdentityRole>(...)`), including password policies, lockout settings, etc.
- **User Model**: The `Models/User.cs` class typically extends `IdentityUser` to add custom properties to the user profile.
- **UI**: Identity provides default UI for login, registration, and account management, which can be scaffolded and customized. Controllers like `AccountController.cs` often handle these actions.

### 6. Logging (Serilog)
Serilog is a popular structured logging library for .NET.
- **Configuration**: Set up in `Program.cs` (`Log.Logger = new LoggerConfiguration()...`, `builder.Host.UseSerilog()`).
- **Sinks**: Configured to write logs to multiple destinations (sinks), such as the console (`WriteTo.Console()`) and rolling files (`WriteTo.File("logs/finance-tracker.txt", ...)`).
- **Structured Logging**: Allows for logging complex data in a structured format (e.g., JSON), making logs easier to query and analyze.

### 7. `HttpClientFactory` and External API Integration
To interact with external APIs (like AlphaVantage, CoinGecko, OpenRouter), `HttpClientFactory` is used.
- **Registration**: In `Program.cs`, named `HttpClient` instances are registered and configured:
  ```csharp
  builder.Services.AddHttpClient("AlphaVantage", client => { /* config */ });
  builder.Services.AddHttpClient("CoinGecko", client => { /* config */ });
  // Potentially one for OpenRouter if used by FinancialAIService
  ```
- **Benefits**: Manages the lifetime of `HttpClientMessageHandler` instances, avoiding common issues like socket exhaustion. Allows for centralized configuration of HTTP clients.
- **Usage**: Services or controllers can request an `IHttpClientFactory` via dependency injection and then create clients by name: `var client = _httpClientFactory.CreateClient("AlphaVantage");`.

### 8. Frontend Technologies
- **Tailwind CSS**: A utility-first CSS framework. Instead of writing custom CSS, you apply pre-defined utility classes directly in your HTML markup. This speeds up UI development and ensures consistency. Managed via npm and often compiled into a final CSS file.
- **Chart.js**: A JavaScript library used to render various types of charts (line, bar, pie, etc.) in the browser. Used in the Analytics section to visualize financial data. Typically included via `libman.json` or npm and referenced in Razor views.
- **JavaScript (`wwwroot/js/`)**: Custom JavaScript files for client-side interactivity, form validation, AJAX calls, and enhancing user experience.

## Getting Started

### Prerequisites
- .NET 8 SDK: [Download .NET](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js and npm (for Tailwind CSS and client-side library management): [Download Node.js](https://nodejs.org/)
- LibMan CLI (for client-side library management, if not using npm for all client libs):
  ```bash
  dotnet tool install -g Microsoft.Web.LibraryManager.Cli
  ```
- PostgreSQL Database Server: [Install PostgreSQL](https://www.postgresql.org/download/)

### Installation

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/yourusername/personal-finance-tracker.git
    cd personal-finance-tracker
    ```

2.  **Restore .NET dependencies:**
    ```bash
    dotnet restore
    ```

3.  **Restore client-side libraries (if using LibMan):**
    Navigate to the project directory (e.g., `PersonalFinanceTracker`):
    ```bash
    cd PersonalFinanceTracker
    libman restore 
    cd .. 
    ```
    (If using npm for frontend dependencies, you would typically run `npm install` in the directory containing `package.json`).

4.  **Set up the database:**
    - Ensure your PostgreSQL server is running.
    - Update the connection string in `PersonalFinanceTracker/appsettings.json` (and `appsettings.Development.json`) with your PostgreSQL credentials:
      ```json
      "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=personal_finance_tracker_db;Username=your_postgres_user;Password=your_postgres_password"
      }
      ```
    - Apply Entity Framework Core migrations to create the database schema:
      ```bash
      cd PersonalFinanceTracker 
      dotnet ef database update
      cd ..
      ```
      (If you encounter issues, you might need to add migrations first if none exist: `dotnet ef migrations add InitialCreate` then `dotnet ef database update`)

5.  **Configure API Keys:**
    - Open `PersonalFinanceTracker/appsettings.json` (or `appsettings.Development.json`).
    - Add your API keys for OpenRouter, CoinGecko, AlphaVantage, etc., under the appropriate sections. Example:
      ```json
      "OpenRouter": {
        "ApiKey": "your_openrouter_api_key_here"
      },
      "MarketAPIs": {
        "CoinGecko": {
          "ApiKey": "your_coingecko_api_key_here" 
        },
        "AlphaVantage": {
          "ApiKey": "your_alphavantage_api_key_here"
        }
        // Add other API keys as needed
      }
      ```
    - **Note**: The CoinGecko free API does not require a key for most public endpoints, but if you are using their demo/pro tier, the key is sent as a header `x-cg-demo-api-key` (as configured in `Program.cs`). The `ApiKey` field in `appsettings.json` for CoinGecko in the example above is for consistency if you choose to manage it there. AlphaVantage typically requires an API key.

6.  **Run the application:**
    ```bash
    cd PersonalFinanceTracker
    dotnet run
    ```
    The application should now be accessible at `https://localhost:port` or `http://localhost:port` as specified in the console output.

## Configuration

### API Keys
To enable the AI Financial Assistant and market data functionalities, you need to configure the respective API keys in `PersonalFinanceTracker/appsettings.json` or `PersonalFinanceTracker/appsettings.Development.json`.

- **OpenRouter API Key** (for AI Financial Assistant):
  ```json
  "OpenRouter": {
    "ApiKey": "sk-or-v1-your_openrouter_api_key_here"
  }
  ```
  Create an account on [OpenRouter](https://openrouter.ai) to get your API key.

- **Market Data API Keys** (e.g., AlphaVantage, CoinGecko):
  ```json
  "MarketAPIs": {
    "AlphaVantage": {
      "ApiKey": "your_alphavantage_api_key"
    },
    "CoinGecko": {
      "ApiKey": "your_coingecko_api_key_if_needed_for_pro_tier" 
    }
    // Add other API keys as needed
  }
  ```
  Obtain keys from their respective websites:
  - [AlphaVantage](https://www.alphavantage.co/support/#api-key)
  - [CoinGecko](https://www.coingecko.com/en/api) (Note: The free tier for CoinGecko often doesn't require an API key for basic endpoints, but a key is needed for their paid/demo tiers.)

### Database Connection
The PostgreSQL connection string is configured in `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=personal_finance_tracker_db;Username=your_postgres_user;Password=your_postgres_password"
}
```
Ensure this matches your PostgreSQL setup.

## License
[MIT License](LICENSE)
