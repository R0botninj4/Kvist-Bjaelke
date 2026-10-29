import { serve } from "bun";
import index from "./index.html";

async function api(request: Request) {
    const url = new URL(request.url);
    const backend = process.env.API_URL ?? "http://localhost:3501";
    try {
        return await fetch(new Request(backend + url.pathname + url.search, request));
    } catch {
        return new Response("API'et kan ikke kontaktes.", { status: 502 });
    }
}

const server = serve({
    hostname: "0.0.0.0",
    port: Number(process.env.PORT ?? 3500),
    routes: {
        "/login": api,
        "/api/*": api,
        "/health": api,
        "/*": index
    },
    development: process.env.NODE_ENV !== "production" && {
        hmr: true,
        console: true
    }
});

console.log(`Frontend: ${server.url}`);
