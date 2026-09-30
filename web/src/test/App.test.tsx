import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { App } from "../App";

function renderWithProviders(ui: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>
  );
}

/**
 * Smoke test — the SPA mounts the top-level layout and the "Kairion" header
 * is visible. The remaining pages are integration-tested end-to-end against
 * the running API.
 */
describe("App", () => {
  it("renders the Kairion header", () => {
    renderWithProviders(<App />);
    expect(screen.getByText("Kairion")).toBeInTheDocument();
    expect(screen.getByText(/evidence-grounded demand research/i)).toBeInTheDocument();
  });
});
