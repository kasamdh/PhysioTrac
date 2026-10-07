import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HelpDialog } from "./HelpDialog";
import { helpKeyFor, pageHelp } from "./helpContent";

describe("helpKeyFor", () => {
  it.each([
    ["/", "home"],
    ["/patients", "patients"],
    ["/admin/patients", "admin-patients"],
    ["/schedule", "schedule"],
    ["/schedule/hours", "provider-hours"],
    ["/providers", "providers"],
    ["/billing", "billing"],
    ["/admin", "admin"],
    ["/admin/users", "users"],
    ["/admin/locations", "locations"],
    ["/admin/messages", "messages"],
    ["/admin/change-password", "change-password"],
  ])("%s -> %s", (path, key) => {
    expect(helpKeyFor(path)).toBe(key);
    expect(pageHelp[key]).toBeDefined();
  });
});

describe("HelpDialog", () => {
  it("shows the page's help with section links and the shared Header section", () => {
    render(<HelpDialog helpKey="admin-patients" onClose={vi.fn()} />);
    expect(screen.getByRole("dialog", { name: "Patient List Help" })).toBeInTheDocument();
    const nav = screen.getByRole("navigation", { name: "Help sections" });
    expect(nav).toHaveTextContent("Add patient");
    expect(nav).toHaveTextContent("Delete and restore");
    expect(nav).toHaveTextContent("Header");
    expect(screen.getByRole("heading", { name: "Header" })).toBeInTheDocument();
    expect(document.body.dataset.helpOpen).toBe("true");
  });

  it("closes with Escape and with the close button", async () => {
    const onClose = vi.fn();
    const { unmount } = render(<HelpDialog helpKey="home" onClose={onClose} />);
    await userEvent.keyboard("{Escape}");
    await userEvent.click(screen.getByRole("button", { name: "Close help" }));
    expect(onClose).toHaveBeenCalledTimes(2);
    unmount();
    expect(document.body.dataset.helpOpen).toBeUndefined();
  });
});
