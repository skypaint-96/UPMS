# UPMS Frontend

React + TypeScript + Vite frontend for the Unified Project Management System.

## Overview

This is the admin UI and report runner interface for the UPMS Snapshot-Based Reporting & Analysis Platform.

## Tech Stack

- **Framework:** React 18
- **Language:** TypeScript
- **Build Tool:** Vite 7
- **Styling:** CSS (Custom CSS variables)

## Project Structure

```
src/Frontend/
├── src/
│   ├── components/         # React components
│   │   └── HealthCheck.tsx # Backend health status component
│   ├── services/           # API service layer
│   │   └── api.ts          # HTTP client & API endpoints
│   ├── types/              # TypeScript type definitions
│   │   └── health.ts       # Health check types
│   ├── App.tsx             # Main application component
│   ├── App.css             # Application styles
│   └── main.tsx            # Application entry point
├── index.html              # HTML template
├── vite.config.ts          # Vite configuration
├── tsconfig.json           # TypeScript configuration
├── .env.example            # Environment variable template
└── package.json            # Dependencies and scripts
```

## Getting Started

### Prerequisites

- Node.js 18+ and npm

### Installation

1. Install dependencies:
   ```bash
   npm install
   ```

2. Create a `.env` file from the example:
   ```bash
   cp .env.example .env
   ```

3. Update the `.env` file with your backend API URL:
   ```
   VITE_API_BASE_URL=http://localhost:3000
   ```

### Development

Run the development server:

```bash
npm run dev
```

The application will be available at `http://localhost:5173`

### Build

Build for production:

```bash
npm run build
```

Preview production build:

```bash
npm run preview
```

## Docker Configuration

The Vite server is configured to work in Docker containers:

- **Host:** `0.0.0.0` (accessible from outside the container)
- **Port:** `5173`
- **API Proxy:** `/api` routes are proxied to the backend service

See [`vite.config.ts`](vite.config.ts:1) for details.

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `VITE_API_BASE_URL` | Backend API base URL | `http://localhost:3000` |

## Features

### Current

- ✅ Backend health check monitoring
- ✅ Connection status display
- ✅ Auto-refresh health status (every 30s)
- ✅ Basic navigation structure
- ✅ Responsive design

### Planned

- Snapshot management interface
- Report generation UI
- Data analysis tools
- User authentication
- Real-time updates

## API Integration

The frontend communicates with the backend via the API service layer:

- **Service:** [`src/services/api.ts`](src/services/api.ts:1)
- **Types:** [`src/types/health.ts`](src/types/health.ts:1)
- **Endpoints:**
  - `GET /health` - Backend health status

## Development Notes

- All API calls use the centralized `api` service
- Type-safe API responses using TypeScript interfaces
- Environment variables are accessed via `import.meta.env.VITE_*`
- CSS uses custom properties for theming

## Troubleshooting

### Port Already in Use

If port 5173 is already in use, update [`vite.config.ts`](vite.config.ts:1):

```typescript
server: {
  port: 3001, // Change to available port
}
```

### Backend Connection Issues

1. Check that the backend is running on the configured port
2. Verify `VITE_API_BASE_URL` in your `.env` file
3. Check browser console for CORS errors
4. Ensure proxy configuration in [`vite.config.ts`](vite.config.ts:1) matches your backend

## Contributing

When adding new features:

1. Create types in `src/types/`
2. Add API methods to `src/services/api.ts`
3. Create components in `src/components/`
4. Update this README with new features

## License

See [`LICENSE`](../../LICENSE:1) file in the project root.
