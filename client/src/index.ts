import { serve } from "bun";
import index from "./index.html";

const server = serve({
    hostname: "0.0.0.0",
    port: Number(process.env.PORT ?? 3500),
    routes: { "/*": index },
    development: process.env.NODE_ENV !== "production" && {
        hmr: true,
        console: true
    }
});

console.log(`Frontend: ${server.url}`);
