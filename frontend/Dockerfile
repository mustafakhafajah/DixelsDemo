# Builds the React app and serves it with nginx. docker-compose.yml at the repo root builds it; see docs/docker.md.

FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .

# Where the app finds the API and the address it lives under. Values set here win over .env.production.
ARG VITE_API_URL=https://localhost:8443
ARG VITE_BASE=/
ENV VITE_API_URL=$VITE_API_URL \
    VITE_BASE=$VITE_BASE
RUN npm run build


FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
