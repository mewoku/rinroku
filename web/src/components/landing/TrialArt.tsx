/** Small crisp SVG illustrations for the three Daily trials. */

export function PatternArt() {
  const a = [1, 0, 1, 0, 1, 0, 1, 0, 1];
  const b = [0, 1, 0, 1, 0, 1, 0, 1, 0];
  const cell = (on: number[], ox: number) =>
    on.map((v, i) => (
      <rect key={`${ox}-${i}`} x={ox + (i % 3) * 9} y={8 + Math.floor(i / 3) * 9} width={8} height={8} fill={v ? "var(--pattern-accent)" : "#2a1450"} />
    ));
  return (
    <svg viewBox="0 0 96 44" className="h-auto w-full" shapeRendering="crispEdges" aria-hidden="true">
      {cell(a, 4)}
      <path d="M40 20h10v-4l6 6-6 6v-4H40z" fill="var(--pattern-accent-2)" />
      {cell(b, 62)}
    </svg>
  );
}

export function SpatialArt() {
  // isometric 3×3 board with two yellow cubes
  const tile = (x: number, y: number, fill: string) => {
    const cx = 48 + (x - y) * 10;
    const cy = 16 + (x + y) * 5;
    return <path key={`t${x}${y}`} d={`M${cx} ${cy}l10 5l-10 5l-10 -5z`} fill={fill} />;
  };
  const cube = (x: number, y: number) => {
    const cx = 48 + (x - y) * 10;
    const cy = 16 + (x + y) * 5 - 10;
    return (
      <g key={`c${x}${y}`}>
        <path d={`M${cx} ${cy}l10 5l-10 5l-10 -5z`} fill="#E8DA37" />
        <path d={`M${cx - 10} ${cy + 5}l10 5v10l-10 -5z`} fill="#c8bb2c" />
        <path d={`M${cx + 10} ${cy + 5}l-10 5v10l10 -5z`} fill="#a89c20" />
      </g>
    );
  };
  const tiles = [];
  for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++) tiles.push(tile(x, y, (x + y) % 2 ? "#135B73" : "#11C5B3"));
  return (
    <svg viewBox="0 0 96 50" className="h-auto w-full" shapeRendering="crispEdges" aria-hidden="true">
      {tiles}
      {cube(1, 0)}
      {cube(2, 1)}
    </svg>
  );
}

export function LinkArt() {
  const n = 5;
  const s = 8;
  const path = [
    [0, 0], [1, 0], [2, 0], [2, 1], [1, 1], [0, 1], [0, 2], [0, 3], [1, 3], [2, 3], [2, 2], [3, 2], [4, 2],
  ];
  const nums: Record<string, string> = { "0,0": "1", "0,2": "2", "2,2": "3", "4,2": "4" };
  return (
    <svg viewBox="0 0 96 44" className="h-auto w-full" shapeRendering="crispEdges" aria-hidden="true">
      <g transform="translate(26 2)">
        {Array.from({ length: n * n }, (_, i) => (
          <rect key={i} x={(i % n) * s} y={Math.floor(i / n) * s} width={s - 1} height={s - 1} fill="#4a1e12" />
        ))}
        {path.map(([x, y], i) => (
          <rect key={`p${i}`} x={x! * s + 1} y={y! * s + 1} width={s - 3} height={s - 3} fill={i === path.length - 1 ? "var(--link-accent-2)" : "var(--link-accent)"} />
        ))}
        {Object.entries(nums).map(([k, v]) => {
          const [x, y] = k.split(",").map(Number);
          return (
            <text key={k} x={x! * s + 3.5} y={y! * s + 5.6} fontSize="5" textAnchor="middle" fill="#07080B" fontFamily="monospace" fontWeight="700">
              {v}
            </text>
          );
        })}
        <rect x={3 * s} y={0} width={s - 1} height={s - 1} fill="#17181c" />
        <rect x={3 * s} y={s} width={s - 1} height={s - 1} fill="#17181c" />
      </g>
    </svg>
  );
}
