import { useState, useCallback } from "react";

const HOME_TEAM = {
  name: "Brugge Bears",
  abbr: "BRU",
  color: "#e85d26",
  players: [
    { num: 4, name: "De Smet", pos: "PG", active: true },
    { num: 7, name: "Janssens", pos: "SG", active: true },
    { num: 11, name: "Peeters", pos: "SF", active: true },
    { num: 23, name: "Vermeer", pos: "PF", active: true },
    { num: 34, name: "Claes", pos: "C", active: true },
    { num: 5, name: "Maes", pos: "PG", active: false },
    { num: 8, name: "Willems", pos: "SF", active: false },
    { num: 14, name: "Dubois", pos: "PF", active: false },
  ],
};

const AWAY_TEAM = {
  name: "Gent Giants",
  abbr: "GNT",
  color: "#2d7dd2",
  players: [
    { num: 3, name: "Mertens", pos: "PG", active: true },
    { num: 10, name: "Bakker", pos: "SG", active: true },
    { num: 15, name: "Van Damme", pos: "SF", active: true },
    { num: 21, name: "Wouters", pos: "PF", active: true },
    { num: 33, name: "Leclercq", pos: "C", active: true },
    { num: 6, name: "Hermans", pos: "SG", active: false },
    { num: 12, name: "Goossens", pos: "PF", active: false },
    { num: 20, name: "Lambert", pos: "SF", active: false },
  ],
};

function is3PointZone(xPct, yPct) {
  const dx = xPct - 50;
  const dy = yPct - 90;
  const dist = Math.sqrt(dx * dx + dy * dy);
  if ((xPct < 12 || xPct > 88) && yPct > 58) return true;
  return dist > 42;
}

export default function ScoringLandscape() {
  const [selectedTeam, setSelectedTeam] = useState("home");
  const [selectedPlayer, setSelectedPlayer] = useState(null);
  const [homeScore, setHomeScore] = useState(0);
  const [awayScore, setAwayScore] = useState(0);
  const [quarter, setQuarter] = useState(1);
  const [clock] = useState("10:00");
  const [log, setLog] = useState([]);
  const [shotChartDots, setShotChartDots] = useState([]);
  const [homeFouls, setHomeFouls] = useState(0);
  const [awayFouls, setAwayFouls] = useState(0);
  const [flashScore, setFlashScore] = useState(null);
  const [pendingShot, setPendingShot] = useState(null);
  const [followUp, setFollowUp] = useState(null);
  const [showLog, setShowLog] = useState(false);
  const [showSubs, setShowSubs] = useState(false);

  const activeTeam = selectedTeam === "home" ? HOME_TEAM : AWAY_TEAM;
  const otherTeamObj = selectedTeam === "home" ? AWAY_TEAM : HOME_TEAM;
  const homeActive = HOME_TEAM.players.filter(p => p.active);
  const awayActive = AWAY_TEAM.players.filter(p => p.active);
  const currentActive = activeTeam.players.filter(p => p.active);

  const addLog = useCallback((msg) => {
    setLog(prev => [{ msg, time: clock, q: quarter, ts: Date.now(), team: selectedTeam }, ...prev].slice(0, 80));
  }, [clock, quarter, selectedTeam]);

  const selectPlayer = useCallback((p, team) => {
    setSelectedTeam(team);
    setSelectedPlayer(prev => prev?.num === p.num && selectedTeam === team ? null : p);
    setPendingShot(null);
    setFollowUp(null);
  }, [selectedTeam]);

  const handleCourtTap = useCallback((e) => {
    if (!selectedPlayer || followUp) return;
    const rect = e.currentTarget.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width * 100);
    const y = ((e.clientY - rect.top) / rect.height * 100);
    setPendingShot({ x: x.toFixed(1), y: y.toFixed(1), suggested: is3PointZone(x, y) ? "3pt" : "2pt" });
  }, [selectedPlayer, followUp]);

  const confirmShot = useCallback((type, result) => {
    if (!pendingShot || !selectedPlayer) return;
    const isHome = selectedTeam === "home";
    const pLabel = `#${selectedPlayer.num} ${selectedPlayer.name}`;
    const pts = type === "3pt" ? 3 : 2;
    const made = result === "made";
    setShotChartDots(prev => [...prev, {
      x: pendingShot.x, y: pendingShot.y, player: selectedPlayer.num,
      made, type, id: Date.now(), color: made ? "#4ade80" : "#f87171",
    }]);
    if (made) {
      if (isHome) setHomeScore(s => s + pts); else setAwayScore(s => s + pts);
      setFlashScore(isHome ? "home" : "away");
      setTimeout(() => setFlashScore(null), 350);
      addLog(`${pLabel} — ${pts}PT Made ✓`);
      setFollowUp("assist");
    } else {
      addLog(`${pLabel} — ${pts}PT Miss ✗`);
      setFollowUp("rebound");
    }
    setPendingShot(null);
  }, [pendingShot, selectedPlayer, selectedTeam, addLog]);

  const handleFollowUp = useCallback((player) => {
    if (!player) { setFollowUp(null); return; }
    addLog(`  ↳ #${player.num} ${player.name} — ${followUp === "assist" ? "Assist" : "Rebound"}`);
    setFollowUp(null);
  }, [followUp, addLog]);

  const handleQuickStat = useCallback((statId) => {
    if (!selectedPlayer) return;
    const pLabel = `#${selectedPlayer.num} ${selectedPlayer.name}`;
    const isHome = selectedTeam === "home";
    const labels = {
      ft_made: "FT Made ✓", ft_miss: "FT Miss ✗",
      oreb: "OFF Rebound", dreb: "DEF Rebound",
      ast: "Assist", stl: "Steal", blk: "Block", to: "Turnover",
      pf: "Personal Foul", tech: "Technical Foul",
    };
    addLog(`${pLabel} — ${labels[statId] || statId}`);
    if (statId === "ft_made") {
      if (isHome) setHomeScore(s => s + 1); else setAwayScore(s => s + 1);
      setFlashScore(isHome ? "home" : "away");
      setTimeout(() => setFlashScore(null), 350);
    }
    if (statId === "pf" || statId === "tech") {
      if (isHome) setHomeFouls(f => f + 1); else setAwayFouls(f => f + 1);
    }
  }, [selectedPlayer, selectedTeam, addLog]);

  const undo = useCallback(() => {
    if (log.length === 0) return;
    const last = log[0];
    if (last.msg.includes("2PT Made")) { if (last.team === "home") setHomeScore(s => Math.max(0, s - 2)); else setAwayScore(s => Math.max(0, s - 2)); }
    else if (last.msg.includes("3PT Made")) { if (last.team === "home") setHomeScore(s => Math.max(0, s - 3)); else setAwayScore(s => Math.max(0, s - 3)); }
    else if (last.msg.includes("FT Made")) { if (last.team === "home") setHomeScore(s => Math.max(0, s - 1)); else setAwayScore(s => Math.max(0, s - 1)); }
    if (last.msg.includes("PT Made") || last.msg.includes("PT Miss")) setShotChartDots(prev => prev.slice(0, -1));
    setLog(prev => prev.slice(1));
    setFollowUp(null);
    setPendingShot(null);
  }, [log]);

  const btn = {
    border: "none",
    cursor: "pointer",
    fontFamily: "inherit",
    transition: "all 0.08s",
    WebkitTapHighlightColor: "transparent",
  };

  // Player card renderer for side panels
  const PlayerCard = ({ p, teamSide, teamColor }) => {
    const isSelected = selectedPlayer?.num === p.num && selectedTeam === teamSide;
    const isActiveTeam = selectedTeam === teamSide;
    return (
      <button
        onClick={() => selectPlayer(p, teamSide)}
        style={{
          ...btn,
          width: "100%",
          display: "flex",
          flexDirection: teamSide === "home" ? "row" : "row-reverse",
          alignItems: "center",
          gap: 6,
          padding: "5px 8px",
          borderRadius: 6,
          background: isSelected ? teamColor : "#141414",
          border: isSelected ? `2px solid ${teamColor}` : "2px solid #1a1a1a",
          color: isSelected ? "#fff" : isActiveTeam ? "#bbb" : "#666",
          marginBottom: 3,
        }}
      >
        <div style={{
          fontSize: 18,
          fontWeight: 800,
          lineHeight: 1,
          minWidth: 28,
          textAlign: "center",
        }}>
          {p.num}
        </div>
        <div style={{
          flex: 1,
          overflow: "hidden",
          textAlign: teamSide === "home" ? "left" : "right",
        }}>
          <div style={{
            fontSize: 9,
            fontWeight: 600,
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
            letterSpacing: 0.3,
          }}>
            {p.name}
          </div>
          <div style={{ fontSize: 7, opacity: 0.5, marginTop: 1 }}>{p.pos}</div>
        </div>
      </button>
    );
  };

  const SCREEN_W = 840;
  const SCREEN_H = 440;

  return (
    <div style={{
      minHeight: "100vh",
      background: "#060606",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      padding: "16px 12px 60px",
      fontFamily: "'SF Mono', 'Fira Code', 'Cascadia Code', Consolas, monospace",
    }}>
      {/* Title */}
      <div style={{ textAlign: "center", marginBottom: 12, maxWidth: 600 }}>
        <div style={{ display: "flex", gap: 8, justifyContent: "center", marginBottom: 4 }}>
          <span style={{ background: "#e85d26", color: "#000", padding: "2px 10px", fontSize: 10, fontWeight: 700, letterSpacing: 2 }}>V3</span>
          <span style={{ color: "#444", fontSize: 10, letterSpacing: 1 }}>LANDSCAPE — COURT-FIRST</span>
        </div>
        <p style={{ color: "#555", fontSize: 10, margin: 0, lineHeight: 1.5 }}>
          Home team left, court center, away team right. Same flow: select player → tap court → confirm shot.
        </p>
      </div>

      {/* Landscape phone frame */}
      <div style={{
        width: SCREEN_W,
        maxWidth: "100%",
        height: SCREEN_H,
        background: "#0a0a0a",
        border: "3px solid #2a2a2a",
        borderRadius: 24,
        overflow: "hidden",
        display: "flex",
        flexDirection: "column",
        boxShadow: "0 24px 80px rgba(0,0,0,0.6), 0 0 0 1px rgba(255,255,255,0.04)",
      }}>
        {/* ── Top scoreboard bar ── */}
        <div style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: 12,
          padding: "4px 16px",
          background: "#0e0e0e",
          borderBottom: "1px solid #1a1a1a",
          flexShrink: 0,
          height: 38,
        }}>
          {/* Home score */}
          <div style={{
            display: "flex", alignItems: "center", gap: 8,
            background: selectedTeam === "home" ? HOME_TEAM.color + "15" : "transparent",
            border: selectedTeam === "home" ? `1px solid ${HOME_TEAM.color}55` : "1px solid transparent",
            borderRadius: 6, padding: "2px 12px",
          }}>
            <span style={{ fontSize: 10, fontWeight: 700, color: HOME_TEAM.color, letterSpacing: 1 }}>{HOME_TEAM.abbr}</span>
            <span style={{
              fontSize: 22, fontWeight: 800, color: "#fff",
              transform: flashScore === "home" ? "scale(1.12)" : "scale(1)", transition: "transform 0.12s",
              fontVariantNumeric: "tabular-nums",
            }}>{homeScore}</span>
            <span style={{ fontSize: 8, color: "#555" }}>F:{homeFouls}</span>
          </div>

          {/* Clock */}
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ fontSize: 8, fontWeight: 700, letterSpacing: 2, color: "#555" }}>Q{quarter}</span>
            <span style={{ fontSize: 16, fontWeight: 700, color: "#ddd", fontVariantNumeric: "tabular-nums" }}>{clock}</span>
            <button onClick={() => setQuarter(q => Math.min(4, q + 1))} style={{ ...btn, background: "#1a1a1a", color: "#666", fontSize: 8, padding: "2px 6px", borderRadius: 3, fontWeight: 700 }}>Q+</button>
            <button onClick={undo} style={{ ...btn, background: "#2a1515", color: "#f87171", fontSize: 8, padding: "2px 6px", borderRadius: 3, fontWeight: 700 }}>UNDO</button>
            <button onClick={() => setShowLog(!showLog)} style={{ ...btn, background: "#151a15", color: "#4ade80", fontSize: 8, padding: "2px 6px", borderRadius: 3, fontWeight: 700 }}>LOG</button>
          </div>

          {/* Away score */}
          <div style={{
            display: "flex", alignItems: "center", gap: 8,
            background: selectedTeam === "away" ? AWAY_TEAM.color + "15" : "transparent",
            border: selectedTeam === "away" ? `1px solid ${AWAY_TEAM.color}55` : "1px solid transparent",
            borderRadius: 6, padding: "2px 12px",
          }}>
            <span style={{ fontSize: 8, color: "#555" }}>F:{awayFouls}</span>
            <span style={{
              fontSize: 22, fontWeight: 800, color: "#fff",
              transform: flashScore === "away" ? "scale(1.12)" : "scale(1)", transition: "transform 0.12s",
              fontVariantNumeric: "tabular-nums",
            }}>{awayScore}</span>
            <span style={{ fontSize: 10, fontWeight: 700, color: AWAY_TEAM.color, letterSpacing: 1 }}>{AWAY_TEAM.abbr}</span>
          </div>
        </div>

        {/* ── Main 3-column layout ── */}
        <div style={{ flex: 1, display: "flex", overflow: "hidden", position: "relative" }}>

          {/* ── LEFT: Home team players ── */}
          <div style={{
            width: 120,
            flexShrink: 0,
            background: "#0c0c0c",
            borderRight: `1px solid ${selectedTeam === "home" ? HOME_TEAM.color + "33" : "#1a1a1a"}`,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}>
            <div style={{
              fontSize: 7, fontWeight: 700, letterSpacing: 1.5, color: HOME_TEAM.color,
              padding: "6px 8px 4px", textAlign: "left", flexShrink: 0,
            }}>
              {HOME_TEAM.name.toUpperCase()}
            </div>
            <div style={{ flex: 1, overflow: "auto", padding: "0 4px 4px" }}>
              {homeActive.map(p => (
                <PlayerCard key={p.num} p={p} teamSide="home" teamColor={HOME_TEAM.color} />
              ))}
              <div style={{ height: 1, background: "#1a1a1a", margin: "4px 0" }} />
              <div style={{ fontSize: 7, color: "#333", letterSpacing: 1, padding: "0 4px 2px" }}>BENCH</div>
              {HOME_TEAM.players.filter(p => !p.active).map(p => (
                <PlayerCard key={p.num} p={p} teamSide="home" teamColor={HOME_TEAM.color} />
              ))}
            </div>
          </div>

          {/* ── CENTER: Court + stats ── */}
          <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", position: "relative" }}>
            {/* Court */}
            <div style={{ flex: 1, position: "relative", minHeight: 0 }}>
              <div
                onClick={handleCourtTap}
                style={{
                  width: "100%", height: "100%",
                  background: "linear-gradient(180deg, #18150f 0%, #141210 100%)",
                  position: "relative",
                  cursor: selectedPlayer && !followUp ? "crosshair" : "default",
                  overflow: "hidden",
                }}
              >
                {/* Court SVG */}
                <svg viewBox="0 0 100 100" preserveAspectRatio="none" style={{ width: "100%", height: "100%", position: "absolute", top: 0, left: 0 }}>
                  <rect x="2" y="2" width="96" height="96" fill="none" stroke="#2a2520" strokeWidth="0.5" />
                  <rect x="31" y="60" width="38" height="38" fill="#1e1a1444" stroke="#2a2520" strokeWidth="0.4" />
                  <circle cx="50" cy="60" r="12" fill="none" stroke="#2a252088" strokeWidth="0.3" strokeDasharray="2,2" />
                  <line x1="31" y1="60" x2="69" y2="60" stroke="#2a2520" strokeWidth="0.4" />
                  <path d="M 10 98 L 10 58 Q 10 20 50 16 Q 90 20 90 58 L 90 98" fill="none" stroke="#3a352e" strokeWidth="0.5" />
                  <path d="M 44 98 Q 44 82 50 80 Q 56 82 56 98" fill="none" stroke="#2a252088" strokeWidth="0.3" />
                  <line x1="43" y1="93" x2="57" y2="93" stroke="#555" strokeWidth="0.6" />
                  <circle cx="50" cy="95" r="1.8" fill="none" stroke="#888" strokeWidth="0.5" />
                  <line x1="2" y1="2" x2="98" y2="2" stroke="#2a2520" strokeWidth="0.5" />
                  <path d="M 38 2 Q 38 14 50 14 Q 62 14 62 2" fill="none" stroke="#2a252088" strokeWidth="0.3" />
                  <text x="50" y="10" textAnchor="middle" fill="#2a2520" fontSize="3.5" fontWeight="700" fontFamily="monospace">3PT ZONE</text>
                  <text x="50" y="72" textAnchor="middle" fill="#2a252088" fontSize="3" fontWeight="600" fontFamily="monospace">PAINT</text>
                </svg>

                {/* Shot dots */}
                {shotChartDots.map(dot => (
                  <div key={dot.id} style={{
                    position: "absolute",
                    left: `${dot.x}%`, top: `${dot.y}%`,
                    transform: "translate(-50%, -50%)",
                    width: dot.made ? 14 : 12, height: dot.made ? 14 : 12,
                    borderRadius: "50%",
                    background: dot.made ? "#4ade8033" : "transparent",
                    border: `2px solid ${dot.color}`,
                    display: "flex", alignItems: "center", justifyContent: "center",
                    fontSize: 7, fontWeight: 800, color: dot.color, pointerEvents: "none",
                  }}>
                    {dot.made ? "✓" : "✗"}
                  </div>
                ))}

                {/* Pending shot pulse */}
                {pendingShot && (
                  <div style={{
                    position: "absolute",
                    left: `${pendingShot.x}%`, top: `${pendingShot.y}%`,
                    transform: "translate(-50%, -50%)",
                  }}>
                    <div style={{
                      width: 20, height: 20, borderRadius: "50%",
                      border: `2px solid ${activeTeam.color}`,
                      background: activeTeam.color + "33",
                      animation: "pulse 0.8s ease-in-out infinite alternate",
                    }} />
                    <style>{`@keyframes pulse { from { transform: scale(1); opacity: 1; } to { transform: scale(1.3); opacity: 0.6; } }`}</style>
                  </div>
                )}

                {/* Empty state */}
                {!selectedPlayer && !pendingShot && shotChartDots.length === 0 && (
                  <div style={{
                    position: "absolute", top: "45%", left: "50%", transform: "translate(-50%, -50%)",
                    textAlign: "center", pointerEvents: "none",
                  }}>
                    <div style={{ fontSize: 18, opacity: 0.1 }}>☝</div>
                    <div style={{ fontSize: 9, color: "#2a2520" }}>Select player, tap court</div>
                  </div>
                )}
              </div>

              {/* Shot confirmation popup */}
              {pendingShot && selectedPlayer && (
                <div style={{
                  position: "absolute",
                  bottom: 6, left: "50%", transform: "translateX(-50%)",
                  width: 260,
                  background: "#1a1714",
                  border: `1px solid ${activeTeam.color}55`,
                  borderRadius: 10,
                  padding: "8px 10px",
                  zIndex: 10,
                  boxShadow: "0 -6px 24px rgba(0,0,0,0.6)",
                }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
                    <div style={{ fontSize: 9, fontWeight: 700, color: "#777" }}>
                      <span style={{ color: activeTeam.color }}>#{selectedPlayer.num}</span> —{" "}
                      <span style={{ color: pendingShot.suggested === "3pt" ? "#fbbf24" : "#60a5fa" }}>
                        {pendingShot.suggested === "3pt" ? "3PT" : "2PT"} ZONE
                      </span>
                    </div>
                    <button onClick={() => setPendingShot(null)} style={{ ...btn, background: "none", color: "#444", fontSize: 12, padding: "0 2px" }}>✕</button>
                  </div>
                  <div style={{ display: "flex", gap: 5 }}>
                    <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 3 }}>
                      <div style={{ fontSize: 7, fontWeight: 700, letterSpacing: 1, color: "#555", textAlign: "center" }}>2PT</div>
                      <button onClick={() => confirmShot("2pt", "made")} style={{ ...btn, background: "#4ade8018", border: "1px solid #4ade8033", color: "#4ade80", borderRadius: 6, padding: "9px 0", fontSize: 12, fontWeight: 800 }}>MADE</button>
                      <button onClick={() => confirmShot("2pt", "miss")} style={{ ...btn, background: "#f8717118", border: "1px solid #f8717133", color: "#f87171", borderRadius: 6, padding: "9px 0", fontSize: 12, fontWeight: 800 }}>MISS</button>
                    </div>
                    <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 3 }}>
                      <div style={{ fontSize: 7, fontWeight: 700, letterSpacing: 1, color: "#555", textAlign: "center" }}>3PT</div>
                      <button onClick={() => confirmShot("3pt", "made")} style={{ ...btn, background: "#4ade8018", border: "1px solid #4ade8033", color: "#4ade80", borderRadius: 6, padding: "9px 0", fontSize: 12, fontWeight: 800 }}>MADE</button>
                      <button onClick={() => confirmShot("3pt", "miss")} style={{ ...btn, background: "#f8717118", border: "1px solid #f8717133", color: "#f87171", borderRadius: 6, padding: "9px 0", fontSize: 12, fontWeight: 800 }}>MISS</button>
                    </div>
                  </div>
                </div>
              )}

              {/* Follow-up prompt */}
              {followUp && selectedPlayer && (
                <div style={{
                  position: "absolute",
                  bottom: 6, left: "50%", transform: "translateX(-50%)",
                  width: 300,
                  background: followUp === "assist" ? "#121a12" : "#1a1a12",
                  border: `1px solid ${followUp === "assist" ? "#4ade8033" : "#fbbf2433"}`,
                  borderRadius: 10,
                  padding: "8px 10px",
                  zIndex: 10,
                  boxShadow: "0 -6px 24px rgba(0,0,0,0.6)",
                }}>
                  <div style={{ fontSize: 8, fontWeight: 700, color: "#555", letterSpacing: 1, marginBottom: 5 }}>
                    {followUp === "assist" ? "ASSISTED BY?" : "REBOUND BY?"}
                  </div>
                  <div style={{ display: "flex", gap: 3, flexWrap: "wrap" }}>
                    {currentActive.filter(p => p.num !== selectedPlayer.num).map(p => (
                      <button key={p.num} onClick={() => handleFollowUp(p)} style={{
                        ...btn, background: "#1e1e1e", border: "1px solid #333",
                        color: "#ccc", borderRadius: 5, padding: "6px 10px",
                        fontSize: 11, fontWeight: 700, flex: 1, minWidth: 44,
                      }}>#{p.num}</button>
                    ))}
                    {followUp === "rebound" && (
                      <button onClick={() => { addLog(`  ↳ ${otherTeamObj.abbr} — Rebound`); setFollowUp(null); }} style={{
                        ...btn, background: otherTeamObj.color + "15", border: `1px solid ${otherTeamObj.color}33`,
                        color: otherTeamObj.color, borderRadius: 5, padding: "6px 10px",
                        fontSize: 9, fontWeight: 700, flex: 1, minWidth: 44,
                      }}>{otherTeamObj.abbr}</button>
                    )}
                    <button onClick={() => setFollowUp(null)} style={{
                      ...btn, background: "#141414", color: "#444",
                      borderRadius: 5, padding: "6px 8px", fontSize: 8, fontWeight: 700,
                    }}>SKIP</button>
                  </div>
                </div>
              )}
            </div>

            {/* ── Quick stats bar ── */}
            <div style={{
              flexShrink: 0,
              background: "#0e0e0e",
              borderTop: "1px solid #1a1a1a",
              padding: "4px 6px",
              display: "flex",
              gap: 3,
              alignItems: "stretch",
            }}>
              {/* FT */}
              <div style={{ display: "flex", gap: 2 }}>
                <div style={{ display: "flex", flexDirection: "column", justifyContent: "center", marginRight: 2 }}>
                  <div style={{ fontSize: 7, fontWeight: 700, letterSpacing: 1, color: "#444" }}>FT</div>
                </div>
                <button onClick={() => handleQuickStat("ft_made")} style={{
                  ...btn, background: "#4ade8012", border: "1px solid #4ade8022",
                  color: "#4ade80", borderRadius: 5, padding: "5px 10px", fontSize: 10, fontWeight: 700,
                }}>✓</button>
                <button onClick={() => handleQuickStat("ft_miss")} style={{
                  ...btn, background: "#f8717112", border: "1px solid #f8717122",
                  color: "#f87171", borderRadius: 5, padding: "5px 10px", fontSize: 10, fontWeight: 700,
                }}>✗</button>
              </div>

              <div style={{ width: 1, background: "#1a1a1a", margin: "0 2px" }} />

              {/* Single-tap stats */}
              {[
                { id: "ast", label: "AST", color: "#60a5fa" },
                { id: "stl", label: "STL", color: "#c084fc" },
                { id: "blk", label: "BLK", color: "#f472b6" },
                { id: "to", label: "TO", color: "#fb923c" },
              ].map(s => (
                <button key={s.id} onClick={() => handleQuickStat(s.id)} style={{
                  ...btn, background: "#141414", border: "1px solid #1e1e1e",
                  color: selectedPlayer ? s.color : "#333",
                  borderRadius: 5, padding: "5px 10px", fontSize: 10, fontWeight: 700, letterSpacing: 0.5,
                }}>{s.label}</button>
              ))}

              <div style={{ width: 1, background: "#1a1a1a", margin: "0 2px" }} />

              {/* Rebounds */}
              <button onClick={() => handleQuickStat("oreb")} style={{
                ...btn, background: "#fbbf2410", border: "1px solid #fbbf2420",
                color: selectedPlayer ? "#fbbf24" : "#333", borderRadius: 5, padding: "5px 8px", fontSize: 9, fontWeight: 700,
              }}>O-RB</button>
              <button onClick={() => handleQuickStat("dreb")} style={{
                ...btn, background: "#60a5fa10", border: "1px solid #60a5fa20",
                color: selectedPlayer ? "#60a5fa" : "#333", borderRadius: 5, padding: "5px 8px", fontSize: 9, fontWeight: 700,
              }}>D-RB</button>

              <div style={{ width: 1, background: "#1a1a1a", margin: "0 2px" }} />

              {/* Fouls */}
              <button onClick={() => handleQuickStat("pf")} style={{
                ...btn, background: "#fbbf2410", border: "1px solid #fbbf2420",
                color: selectedPlayer ? "#fbbf24" : "#333", borderRadius: 5, padding: "5px 8px", fontSize: 9, fontWeight: 700,
              }}>PF</button>
              <button onClick={() => handleQuickStat("tech")} style={{
                ...btn, background: "#f8717110", border: "1px solid #f8717120",
                color: selectedPlayer ? "#f87171" : "#333", borderRadius: 5, padding: "5px 8px", fontSize: 9, fontWeight: 700,
              }}>TF</button>
            </div>
          </div>

          {/* ── RIGHT: Away team players ── */}
          <div style={{
            width: 120,
            flexShrink: 0,
            background: "#0c0c0c",
            borderLeft: `1px solid ${selectedTeam === "away" ? AWAY_TEAM.color + "33" : "#1a1a1a"}`,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}>
            <div style={{
              fontSize: 7, fontWeight: 700, letterSpacing: 1.5, color: AWAY_TEAM.color,
              padding: "6px 8px 4px", textAlign: "right", flexShrink: 0,
            }}>
              {AWAY_TEAM.name.toUpperCase()}
            </div>
            <div style={{ flex: 1, overflow: "auto", padding: "0 4px 4px" }}>
              {awayActive.map(p => (
                <PlayerCard key={p.num} p={p} teamSide="away" teamColor={AWAY_TEAM.color} />
              ))}
              <div style={{ height: 1, background: "#1a1a1a", margin: "4px 0" }} />
              <div style={{ fontSize: 7, color: "#333", letterSpacing: 1, padding: "0 4px 2px", textAlign: "right" }}>BENCH</div>
              {AWAY_TEAM.players.filter(p => !p.active).map(p => (
                <PlayerCard key={p.num} p={p} teamSide="away" teamColor={AWAY_TEAM.color} />
              ))}
            </div>
          </div>

          {/* ── Log overlay ── */}
          {showLog && (
            <div style={{
              position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
              background: "#0a0a0aee", zIndex: 20,
              overflow: "auto", padding: "10px 16px",
            }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                <div style={{ fontSize: 9, fontWeight: 700, letterSpacing: 2, color: "#555" }}>PLAY-BY-PLAY ({log.length})</div>
                <button onClick={() => setShowLog(false)} style={{ ...btn, background: "#222", color: "#888", fontSize: 9, padding: "4px 10px", borderRadius: 4 }}>CLOSE</button>
              </div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 0 }}>
                {log.map((entry, i) => (
                  <div key={entry.ts + i} style={{
                    width: "50%", padding: "4px 8px 4px 0", display: "flex", gap: 6,
                    borderBottom: "1px solid #141414",
                  }}>
                    <div style={{ fontSize: 8, color: "#444", fontVariantNumeric: "tabular-nums", minWidth: 36, flexShrink: 0 }}>Q{entry.q} {entry.time}</div>
                    <div style={{
                      width: 5, height: 5, borderRadius: 3, flexShrink: 0, marginTop: 3,
                      background: entry.team === "home" ? HOME_TEAM.color : AWAY_TEAM.color,
                    }} />
                    <div style={{ fontSize: 9, color: "#888", lineHeight: 1.4 }}>{entry.msg}</div>
                  </div>
                ))}
              </div>
              {log.length === 0 && <div style={{ color: "#333", fontSize: 11, textAlign: "center", marginTop: 30 }}>No plays yet</div>}
            </div>
          )}
        </div>
      </div>

      {/* ─── Design notes ─── */}
      <div style={{
        maxWidth: 600, marginTop: 24, padding: "18px 20px",
        background: "#111", border: "1px solid #1e1e1e", borderRadius: 8,
        fontFamily: "'Courier New', monospace",
      }}>
        <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: 2, color: "#e85d26", marginBottom: 10 }}>
          LANDSCAPE DESIGN NOTES
        </div>
        {[
          { label: "3-column layout", desc: "Home players (left) — Court + stats (center) — Away players (right). Both rosters are always visible. No team-switching needed, just tap any player from either side." },
          { label: "Court is the hero", desc: "The court occupies the maximum available space in the center. The stat bar sits below it compactly. The court is taller in landscape which gives better shot location accuracy." },
          { label: "Player panels include bench", desc: "Active 5 on top, bench below a divider. Selecting a bench player could trigger a substitution flow in the real app." },
          { label: "Mirrored alignment", desc: "Home names left-aligned, away names right-aligned. Jersey numbers face outward on both sides. Feels natural — like looking at a scorebook." },
          { label: "Same interaction model", desc: "Identical flow as the vertical V2: tap player → tap court → confirm shot. Follow-up prompts and stat bar work the same way." },
          { label: "Both teams always accessible", desc: "Unlike V2 where you had to switch teams, in landscape you can tap any player from either team instantly. This eliminates a full interaction step." },
          { label: "Log as 2-column overlay", desc: "In landscape, the play-by-play log uses a 2-column layout to show more entries at once." },
        ].map((item, i) => (
          <div key={i} style={{ padding: "6px 0", borderTop: i > 0 ? "1px solid #1a1a1a" : "none" }}>
            <span style={{ fontSize: 11, fontWeight: 700, color: "#ddd" }}>{item.label}: </span>
            <span style={{ fontSize: 11, color: "#777", lineHeight: 1.5 }}>{item.desc}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
