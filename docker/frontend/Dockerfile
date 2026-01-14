# =============================================================================
# UPMS Frontend - React + TypeScript + Vite (Development)
# =============================================================================
# Development container with hot reload support
# =============================================================================

FROM node:20-alpine

WORKDIR /app

# Install dependencies first (for better caching)
COPY src/Frontend/package*.json ./
RUN npm install

# Copy source files
COPY src/Frontend/ .

# Expose Vite dev server port
EXPOSE 3000

# Start Vite development server with host binding
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "3000"]
