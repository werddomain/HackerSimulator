const ACCENTS = {
    green: { accent: "#42d392", subtle: "#173b2c" },
    cyan: { accent: "#22d3ee", subtle: "#164e63" },
    purple: { accent: "#a78bfa", subtle: "#3b2a5e" },
};

export function applyAccent(name) {
    const palette = ACCENTS[name] ?? ACCENTS.green;
    document.documentElement.style.setProperty("--hos-accent", palette.accent);
    document.documentElement.style.setProperty("--hos-accent-subtle", palette.subtle);
}

export function applyAnimations(enabled) {
    document.documentElement.classList.toggle("hos-animations-disabled", !enabled);
}
