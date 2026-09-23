FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite bakes VITE_* vars into the built JS at build time, not runtime -- this
# must be the URL the BROWSER can reach (the host's mapped port for the api
# service), not an internal Docker Compose service name.
ARG VITE_API_BASE_URL=http://localhost:5080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

# nginx:alpine has wget but not curl -- unlike the .NET services' Dockerfiles,
# which install curl explicitly for their HEALTHCHECK.
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=6 \
    CMD ["wget", "-q", "-O", "-", "http://localhost:8080/"]
