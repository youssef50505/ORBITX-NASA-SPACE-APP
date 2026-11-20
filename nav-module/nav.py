"""MARS SENTINEL - Navigation & Hazard Detection (stdlib only).
Input: jez_ctx_20m_grid.json (real USGS CTX DEM, 80 m cells). Every tunable number is in CONFIG."""
import json, math, heapq

CONFIG = dict(            # ALL are ASSUMPTIONS (change freely, justify with NASA numbers)
    MAX_SLOPE_DEG=25.0,   # cells steeper than this are impassable
    ROUGH_REF_M=4.0,      # roughness (m) treated as "fully rough"
    W_SLOPE=8.0,          # safest mode: penalty weight on slope
    W_ROUGH=4.0,          # safest mode: penalty weight on roughness (ROCK PROXY, DEM cannot see rocks)
    SPEED_MS=0.8,         # EVA walking speed for ETA
    SIMPLIFY_M=120.0,     # waypoint simplification tolerance
)

def load_grid(path):
    d = json.load(open(path)); z = d["elevations_m"]
    return dict(z=z, rows=len(z), cols=len(z[0]), cell=float(d["mesh_sample_spacing_m"]), meta=d)

def hazards(g):
    """slope (deg, central differences) and roughness (m, |z - mean of 8 neighbours|) per cell."""
    z, R, C, h = g["z"], g["rows"], g["cols"], g["cell"]
    slope = [[0.0] * C for _ in range(R)]; rough = [[0.0] * C for _ in range(R)]
    for r in range(R):
        r0, r1 = max(r - 1, 0), min(r + 1, R - 1)
        for c in range(C):
            c0, c1 = max(c - 1, 0), min(c + 1, C - 1)
            gx = (z[r][c1] - z[r][c0]) / ((c1 - c0) * h); gy = (z[r1][c] - z[r0][c]) / ((r1 - r0) * h)
            slope[r][c] = math.degrees(math.atan(math.hypot(gx, gy)))
            nb = [z[a][b] for a in range(r0, r1 + 1) for b in range(c0, c1 + 1) if (a, b) != (r, c)]
            rough[r][c] = abs(z[r][c] - sum(nb) / len(nb))
    g["slope"], g["rough"] = slope, rough
    g["risk"] = [[0.6 * min(slope[r][c] / CONFIG["MAX_SLOPE_DEG"], 1) + 0.4 * min(rough[r][c] / CONFIG["ROUGH_REF_M"], 1)
                  for c in range(C)] for r in range(R)]
    return g

def astar(g, start, goal, mode="safest"):
    """8-connected A*. step cost = 3D distance * (1 + w_s*(slope/max)^2 + w_r*rough/ref). Returns [(r,c)...] or None."""
    k = CONFIG; ws, wr = (k["W_SLOPE"], k["W_ROUGH"]) if mode == "safest" else (0.0, 0.0)
    z, S, Ro, R, C, h = g["z"], g["slope"], g["rough"], g["rows"], g["cols"], g["cell"]
    if S[start[0]][start[1]] > k["MAX_SLOPE_DEG"] or S[goal[0]][goal[1]] > k["MAX_SLOPE_DEG"]: return None
    hf = lambda r, c: h * math.hypot(r - goal[0], c - goal[1])   # admissible: 2D distance <= 3D cost
    best = {start: 0.0}; prev = {}; pq = [(hf(*start), 0.0, start)]
    while pq:
        _, gc, cur = heapq.heappop(pq)
        if cur == goal:
            path = [cur]
            while cur in prev: cur = prev[cur]; path.append(cur)
            return path[::-1]
        if gc > best.get(cur, 1e18): continue
        r, c = cur
        for dr in (-1, 0, 1):
            for dc in (-1, 0, 1):
                a, b = r + dr, c + dc
                if (dr or dc) and 0 <= a < R and 0 <= b < C and S[a][b] <= k["MAX_SLOPE_DEG"]:
                    d = math.sqrt((dr * h) ** 2 + (dc * h) ** 2 + (z[a][b] - z[r][c]) ** 2)
                    n = gc + d * (1 + ws * (S[a][b] / k["MAX_SLOPE_DEG"]) ** 2 + wr * min(Ro[a][b] / k["ROUGH_REF_M"], 1))
                    if n < best.get((a, b), 1e18):
                        best[(a, b)] = n; prev[(a, b)] = cur; heapq.heappush(pq, (n + hf(a, b), n, (a, b)))
    return None

def dp(pts, tol):
    """Douglas-Peucker on (x,y,i) points; returns kept points."""
    if len(pts) < 3: return pts
    (x1, y1, _), (x2, y2, _) = pts[0], pts[-1]; L = math.hypot(x2 - x1, y2 - y1) or 1e-9
    dist = [abs((x2 - x1) * (y1 - p[1]) - (x1 - p[0]) * (y2 - y1)) / L for p in pts[1:-1]]
    m = max(dist)
    if m <= tol: return [pts[0], pts[-1]]
    i = dist.index(m) + 1
    return dp(pts[:i + 1], tol)[:-1] + dp(pts[i:], tol)

def route_json(g, path, mode):
    """x = metres east of grid west edge, y = metres north of grid south edge, z = elevation (m)."""
    h, R, z, S, K = g["cell"], g["rows"], g["z"], g["slope"], g["risk"]
    xyz = lambda r, c: (c * h, (R - 1 - r) * h, z[r][c])
    dist = sum(math.dist(xyz(*a), xyz(*b)) for a, b in zip(path, path[1:]))
    wp = [p[2] for p in dp([(xyz(*q)[0], xyz(*q)[1], i) for i, q in enumerate(path)], CONFIG["SIMPLIFY_M"])]
    pt = lambda i: dict(zip("xyz", (round(v, 1) for v in xyz(*path[i]))), slopeDeg=round(S[path[i][0]][path[i][1]], 1),
                        risk=round(K[path[i][0]][path[i][1]], 3), col=path[i][1], row=path[i][0])
    return dict(mode=mode, start=pt(0), destination=pt(len(path) - 1), waypoints=[pt(i) for i in wp],
                distanceMeters=round(dist, 1), etaMinutes=round(dist / CONFIG["SPEED_MS"] / 60, 1),
                maxSlopeDeg=round(max(S[r][c] for r, c in path), 1),
                meanRisk=round(sum(K[r][c] for r, c in path) / len(path), 3),
                path=[[c, r] for r, c in path])
