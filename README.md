# Personal Finance Tracker

A comprehensive personal finance management web application built with ASP.NET Core and Tailwind CSS. This application helps users manage their finances, track spending, analyze financial data, monitor market trends, and get AI-powered financial insights.

## Features

### Core Financial Management
- **Dashboard**: Overview of financial status, recent transactions, and account balances
- **Accounts**: Manage multiple bank and investment accounts
- **Transactions**: Record, categorize, and manage income and expenses
- **Categories**: Organize transactions by customizable categories

### Analytics
- **Financial Insights**: Visual breakdown of spending by category
- **Spending Trends**: Track spending patterns over time
- **Account Analysis**: Detailed view of individual account activity
- **Predictions**: AI-powered predictions of future expenses

### Markets
- **Market Overview**: Real-time data on stock indices, cryptocurrencies, forex, and commodities
- **Stock Details**: Comprehensive stock information with historical price charts
- **Cryptocurrency Details**: Detailed cryptocurrency data and price history
- **Forex Details**: Currency pair exchange rates and historical trends
- **Commodity Details**: Commodity prices, details, and historical data

### AI Financial Assistant
- **Chat Interface**: Conversational AI assistant for financial advice
- **Personalized Insights**: AI-generated insights based on user's financial data
- **Suggested Questions**: Dynamic follow-up suggestions based on conversation context
- **OpenRouter Integration**: Powered by Claude 3 Haiku for intelligent financial guidance

## Technical Implementation

### Backend
- **ASP.NET Core MVC**: Framework for building the web application
- **Entity Framework Core**: ORM for database interactions
- **SQLite**: Lightweight database for storing financial data
- **Identity Framework**: User authentication and authorization

### Frontend
- **Tailwind CSS**: Utility-first CSS framework for styling
- **Chart.js**: JavaScript library for interactive data visualization
- **Responsive Design**: Mobile-friendly interface that works across devices

### APIs and Integration
- **OpenRouter API**: Integration with Claude 3 Haiku for AI-powered financial assistance
- **Financial Data**: Currently using simulated financial data (ready for real API integration)

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js and npm (for Tailwind CSS)

### Installation

1. Clone the repository
```
git clone https://github.com/yourusername/personal-finance-tracker.git
cd personal-finance-tracker
```

2. Restore dependencies
```
dotnet restore
```

3. Set up the database
```
dotnet ef database update
```

4. Configure OpenRouter API
   - Create an account on [OpenRouter](https://openrouter.ai)
   - Get your API key
   - Update the API key in `appsettings.json`

5. Run the application
```
dotnet run
```

## Configuration

### OpenRouter API
To enable the AI Financial Assistant with real AI responses:
1. Replace the placeholder API key in `appsettings.json`:
```json
"OpenRouter": {
  "ApiKey": "your_api_key_here"
}
```

## Future Enhancements
- Real financial data API integration
- Budget planning and goal tracking features
- Mobile application
- Export/import functionality for financial data
- Enhanced AI capabilities with personalized financial planning

## License
[MIT License](LICENSE)
