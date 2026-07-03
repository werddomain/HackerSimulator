/*
 * Collocated JavaScript module for the System Monitor application.
 * Produces pseudo-random resource samples. Keeping the sampler in a scoped
 * ".razor.js" file honours the ecosystem rule of no inline scripts.
 */

/**
 * Returns a single resource reading.
 * @returns {{cpu:number, mem:number, net:number, jitter:number[]}}
 */
export function sample() {
    const rand = (min, max) => Math.floor(min + Math.random() * (max - min));
    return {
        cpu: rand(5, 95),
        mem: rand(30, 85),
        net: rand(0, 2048),
        jitter: [rand(-3, 4), rand(-2, 3), rand(-4, 5), rand(-1, 2), rand(-1, 2)]
    };
}
