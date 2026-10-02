FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:alpine
# The base image ships pcre2 10.48, which has a HIGH vulnerability that 10.49 fixes. Drop this when the base image updates.
USER root
RUN apk upgrade --no-cache pcre2
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
USER 101
