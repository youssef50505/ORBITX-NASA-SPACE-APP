import math, unittest, nav
def grid(R, C, f, cell=80.0):
    g = dict(z=[[f(r, c) for c in range(C)] for r in range(R)], rows=R, cols=C, cell=cell, meta={}); return nav.hazards(g)
class T(unittest.TestCase):
    def test_flat_is_straight(self):
        g = grid(20, 20, lambda r, c: 0.0); p = nav.astar(g, (10, 0), (10, 19), "shortest")
        self.assertEqual(len(p), 20); self.assertTrue(all(r == 10 for r, _ in p))
    def test_steep_wall_avoided(self):
        g = grid(21, 21, lambda r, c: 2000.0 if c == 10 and r != 18 else 0.0)   # wall with one gap
        for m in ("shortest", "safest"):
            p = nav.astar(g, (2, 2), (2, 18), m); self.assertIn((18, 10), p)
    def test_unreachable_is_none(self):
        g = grid(15, 15, lambda r, c: 2000.0 if c == 7 else 0.0); self.assertIsNone(nav.astar(g, (7, 1), (7, 13), "safest"))
    def test_safest_not_steeper_than_shortest(self):
        g = grid(40, 40, lambda r, c: 150 * math.exp(-((r - 20) ** 2 + (c - 20) ** 2) / (2 * 4 ** 2)))   # passable hill
        a = nav.route_json(g, nav.astar(g, (20, 2), (20, 37), "shortest"), "shortest")
        b = nav.route_json(g, nav.astar(g, (20, 2), (20, 37), "safest"), "safest")
        self.assertLess(b["maxSlopeDeg"], a["maxSlopeDeg"]); self.assertGreaterEqual(b["distanceMeters"], a["distanceMeters"])
if __name__ == "__main__": unittest.main(verbosity=2)
