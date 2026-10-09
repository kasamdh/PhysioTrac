import "@testing-library/jest-dom/vitest";
import { configure } from "@testing-library/react";

// findBy*/waitFor give up after 1s by default -- too short when the whole
// suite runs in parallel on a busy machine (see testTimeout in vite.config).
configure({ asyncUtilTimeout: 10_000 });
