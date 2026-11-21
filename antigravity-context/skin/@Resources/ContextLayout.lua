-- ==============================================================================
-- Antigravity Context HUD v1.1 - High-Readability Multi-Chat Context Monitor
-- Rainmeter Lua 5.1. UTF-8 without BOM.
-- ==============================================================================

local MAX_SLOTS = 8
local OWNED = {}

local G = {
    SKIN_W     = 360,
    PAD        = 18,
    BAR_W      = 310,
    HEADER_H   = 46,
    FOOTER_H   = 38,
    POLL_SEC   = 4,
}

local themes = {
    obsidian = {
        name = "OBSIDIAN",
        bgRgb = "12,15,24", borderRgb = "255,255,255", borderAlpha = "26",
        titleRgb = "248,250,252", footerRgb = "226,232,240", heroRgb = "255,255,255",
        labelRgb = "226,232,240", subRgb = "203,213,225", mutedRgb = "168,182,202",
        greenRgb = "52,211,153", amberRgb = "251,191,36", redRgb = "248,113,113",
        pillTextRgb = "226,232,240", btnTextRgb = "255,255,255",
        vars = {
            ColorBarTrack        = "255,255,255,28",
            ColorDivider         = "255,255,255,20",
            ColorPillBg          = "255,255,255,20",
            ColorPillBorder      = "255,255,255,40",
            ColorPillText        = "226,232,240,230",
            ColorPillBgHover     = "255,255,255,44",
            ColorPillBorderHover = "255,255,255,85",
            ColorPillTextHover   = "255,255,255,255",
            ColorBtnBg           = "255,255,255,20",
            ColorBtnBorder       = "255,255,255,40",
            ColorBtnText         = "255,255,255,235",
            ColorBtnBgHover      = "255,255,255,44",
            ColorBtnBorderHover  = "255,255,255,85",
            ColorBtnTextHover    = "255,255,255,255",
        },
    },
    frost = {
        name = "FROST",
        bgRgb = "246,248,251", borderRgb = "255,255,255", borderAlpha = "110",
        titleRgb = "15,23,42", footerRgb = "30,41,59", heroRgb = "15,23,42",
        labelRgb = "51,65,85", subRgb = "51,65,85", mutedRgb = "71,85,105",
        greenRgb = "5,150,105", amberRgb = "217,119,6", redRgb = "220,38,38",
        pillTextRgb = "15,23,42", btnTextRgb = "15,23,42",
        vars = {
            ColorBarTrack        = "100,116,139,34",
            ColorDivider         = "100,116,139,30",
            ColorPillBg          = "255,255,255,45",
            ColorPillBorder      = "15,23,42,40",
            ColorPillText        = "15,23,42,225",
            ColorPillBgHover     = "255,255,255,85",
            ColorPillBorderHover = "15,23,42,85",
            ColorPillTextHover   = "15,23,42,255",
            ColorBtnBg           = "255,255,255,45",
            ColorBtnBorder       = "15,23,42,40",
            ColorBtnText         = "15,23,42,235",
            ColorBtnBgHover      = "255,255,255,85",
            ColorBtnBorderHover  = "15,23,42,85",
            ColorBtnTextHover    = "15,23,42,255",
        },
    },
}

local ACCENT_VAR = { Green = "ColorAccentGreen", Amber = "ColorAccentAmber", Red = "ColorAccentRed" }
local PCT_ACCENT_VAR = { Green = "ColorPctGreen", Amber = "ColorPctAmber", Red = "ColorPctRed" }

local state = { theme = "obsidian" }
local chats = {}
local lastSyncTime = os.time()
local nextPoll = os.time() + 1

local function persist(key, val)
    local stateFile = SKIN:GetVariable("@") .. "UserState.inc"
    SKIN:Bang("!WriteKeyValue", "Variables", key, tostring(val), stateFile)
    SKIN:Bang("!SetVariable", key, tostring(val))
end

local function setOpt(meter, opt, value) SKIN:Bang("!SetOption", meter, opt, tostring(value)) end
local function setY(meter, y) SKIN:Bang("!SetOption", meter, "Y", tostring(math.floor(y + 0.5))) end
local function setX(meter, x) SKIN:Bang("!SetOption", meter, "X", tostring(math.floor(x + 0.5))) end
local function show(meter) SKIN:Bang("!ShowMeter", meter) end
local function hide(meter) SKIN:Bang("!HideMeter", meter) end

local function varNum(name, fb)
    local v = tonumber(SKIN:GetVariable(name, tostring(fb or 0)))
    if not v then return fb or 0 end
    return math.max(0, math.min(1, v))
end

local function healthOfUsed(usedFrac)
    if usedFrac >= 0.80 then return "Red" end
    if usedFrac >= 0.50 then return "Amber" end
    return "Green"
end

local function buildOwned()
    OWNED = {
        "MeterChatsEmpty",
        "MeterFooterDivider", "MeterFooterDot", "MeterFooterStatus",
        "MeterFooterSyncPill", "MeterFooterSyncBtn", "MeterFooterSyncClickZone",
    }
    for i = 1, MAX_SLOTS do
        local p = "MeterChat" .. i
        for _, s in ipairs({ "Dot", "Title", "Val", "Sub", "Age", "Bar", "Divider" }) do
            table.insert(OWNED, p .. s)
        end
    end
end

local function hideEverything()
    for _, m in ipairs(OWNED) do
        hide(m)
        setY(m, 0)
    end
end

local function applyTheme()
    local t = themes[state.theme] or themes.obsidian
    local bgAlpha = tonumber(SKIN:GetVariable("WidgetAlpha", "235")) or 235

    SKIN:Bang("!SetVariable", "ColorBgHud", t.bgRgb .. "," .. bgAlpha)
    SKIN:Bang("!SetVariable", "ColorBorderHud", t.borderRgb .. "," .. t.borderAlpha)

    SKIN:Bang("!SetVariable", "ColorTextHero", t.heroRgb .. ",250")
    SKIN:Bang("!SetVariable", "ColorTextTitle", t.titleRgb .. ",240")
    SKIN:Bang("!SetVariable", "ColorPctHero", t.heroRgb .. ",250")
    SKIN:Bang("!SetVariable", "ColorPctGreen", t.greenRgb .. ",245")
    SKIN:Bang("!SetVariable", "ColorPctAmber", t.amberRgb .. ",245")
    SKIN:Bang("!SetVariable", "ColorPctRed", t.redRgb .. ",245")

    SKIN:Bang("!SetVariable", "ColorTextFooter", t.footerRgb .. ",230")
    SKIN:Bang("!SetVariable", "ColorTextLabel", t.labelRgb .. ",230")
    SKIN:Bang("!SetVariable", "ColorTextSub", t.subRgb .. ",225")
    SKIN:Bang("!SetVariable", "ColorTextMuted", t.mutedRgb .. ",210")

    SKIN:Bang("!SetVariable", "ColorAccentGreen", t.greenRgb .. ",240")
    SKIN:Bang("!SetVariable", "ColorAccentAmber", t.amberRgb .. ",240")
    SKIN:Bang("!SetVariable", "ColorAccentRed", t.redRgb .. ",240")

    for k, v in pairs(t.vars) do
        SKIN:Bang("!SetVariable", k, v)
    end

    setOpt("MeterHeaderTitle", "FontColor", t.titleRgb .. ",240")
    setOpt("MeterHeaderCollapseBtn", "FontColor", t.titleRgb .. ",230")
    setOpt("MeterFooterStatus", "FontColor", t.footerRgb .. ",230")

    setOpt("MeterHeaderTopBtn", "FontColor", t.pillTextRgb .. ",235")
    setOpt("MeterHeaderTopPill", "Shape",
        "Rectangle 0,0,48,20,10 | Fill Color " .. t.vars.ColorPillBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorPillBorder)

    setOpt("MeterHeaderThemeBtn", "Text", t.name)
    setOpt("MeterHeaderThemeBtn", "FontColor", t.pillTextRgb .. ",235")
    setOpt("MeterHeaderThemePill", "Shape",
        "Rectangle 0,0,64,20,10 | Fill Color " .. t.vars.ColorPillBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorPillBorder)

    setOpt("MeterFooterSyncBtn", "FontColor", t.btnTextRgb .. ",235")
    setOpt("MeterFooterSyncPill", "Shape",
        "Rectangle 0,0,28,20,10 | Fill Color " .. t.vars.ColorBtnBg .. " | StrokeWidth 0.75 | Stroke Color " .. t.vars.ColorBtnBorder)
end

local function barTrack(w)
    return "Rectangle 0,0," .. w .. ",3.5,1.75 | Fill Color #ColorBarTrack# | StrokeWidth 0"
end

local function barFill(w, health)
    local fw = math.max(3, math.floor(w + 0.5))
    return "Rectangle 0,0," .. fw .. ",3.5,1.75 | Fill Color #" .. ACCENT_VAR[health] .. "# | StrokeWidth 0"
end

local function getTopN()
    local n = tonumber(SKIN:GetVariable("TopChatCount", "3")) or 3
    if n ~= 3 and n ~= 5 and n ~= 8 then n = 3 end
    return n
end

local function isCollapsed()
    local v = tostring(SKIN:GetVariable("Collapsed", "0"))
    return (v == "1" or v == "true")
end

local function loadChatsFromVars()
    local ep = tonumber(SKIN:GetVariable("LastSyncEpoch", "0")) or 0
    if ep > 0 then lastSyncTime = ep end

    local rawCount = tonumber(SKIN:GetVariable("ChatCount", "0")) or 0
    local count = math.max(0, math.min(MAX_SLOTS, rawCount))
    chats = {}
    for i = 1, count do
        local p = "Chat" .. i
        table.insert(chats, {
            idx         = i,
            id          = SKIN:GetVariable(p .. "Id", ""),
            title       = SKIN:GetVariable(p .. "Title", "Conversation " .. i),
            model       = SKIN:GetVariable(p .. "Model", "Gemini"),
            tokens      = tonumber(SKIN:GetVariable(p .. "Tokens", "0")) or 0,
            tokensFmt   = SKIN:GetVariable(p .. "TokensFmt", "0"),
            windowFrac  = varNum(p .. "WindowFrac", 0),
            windowPct   = tonumber(SKIN:GetVariable(p .. "WindowPct", "0")) or 0,
            windowLabel = SKIN:GetVariable(p .. "WindowLabel", "1M"),
            compacts    = tonumber(SKIN:GetVariable(p .. "Compacts", "0")) or 0,
            age         = SKIN:GetVariable(p .. "Age", "--"),
        })
    end
end

local function updateFooterText()
    local diff = os.time() - lastSyncTime
    local str = ""
    if diff < 10 then str = "Live 4s"
    elseif diff < 60 then str = "Updated " .. diff .. "s ago"
    elseif diff < 3600 then str = "Updated " .. math.floor(diff / 60) .. "m ago"
    else str = "Updated " .. math.floor(diff / 3600) .. "h ago" end

    local shown = isCollapsed() and math.min(#chats, 1) or math.min(#chats, getTopN())
    local chatWord = (shown == 1) and "active chat" or "active chats"
    setOpt("MeterFooterStatus", "Text", str .. "  /  " .. shown .. " " .. chatWord)
end

local function layout()
    hideEverything()

    local topN = getTopN()
    local collapsed = isCollapsed()

    setOpt("MeterHeaderTopBtn", "Text", "TOP " .. topN)
    setOpt("MeterHeaderCollapseBtn", "Text", collapsed and ">" or "v")

    local y = G.HEADER_H

    if #chats == 0 then
        setY("MeterChatsEmpty", y + 6)
        show("MeterChatsEmpty")
        y = y + 34
    else
        local limit = collapsed and 1 or math.min(#chats, topN)
        for i = 1, limit do
            local c = chats[i]
            local cp = "MeterChat" .. i

            local frac = c.windowFrac
            local h = healthOfUsed(frac)

            setY(cp .. "Dot", y + 9)
            setOpt(cp .. "Dot", "Shape", "Ellipse 0,0,2.8 | Fill Color #" .. ACCENT_VAR[h] .. "# | StrokeWidth 0")
            show(cp .. "Dot")

            setY(cp .. "Title", y)
            setOpt(cp .. "Title", "Text", "#Chat" .. i .. "Title#")
            local tip = c.title .. " | " .. c.tokensFmt .. " / " .. c.windowLabel .. " (" .. c.windowPct .. "%) | Model: " .. c.model .. " | Compactions: " .. c.compacts .. " | Active: " .. c.age
            setOpt(cp .. "Title", "ToolTipText", tip)
            show(cp .. "Title")

            setY(cp .. "Val", y - 1)
            setOpt(cp .. "Val", "Text", "#Chat" .. i .. "WindowValText#")
            setOpt(cp .. "Val", "FontColor", "#" .. PCT_ACCENT_VAR[h] .. "#")
            show(cp .. "Val")

            setY(cp .. "Sub", y + 21)
            setOpt(cp .. "Sub", "Text", "#Chat" .. i .. "WindowSubText#")
            show(cp .. "Sub")

            setY(cp .. "Age", y + 21)
            setOpt(cp .. "Age", "Text", c.age)
            show(cp .. "Age")

            setY(cp .. "Bar", y + 41)
            setOpt(cp .. "Bar", "Shape", barTrack(G.BAR_W))
            setOpt(cp .. "Bar", "Shape2", barFill(frac * G.BAR_W, h))
            show(cp .. "Bar")

            if i < limit then
                setY(cp .. "Divider", y + 52)
                show(cp .. "Divider")
                y = y + 60
            else
                y = y + 50
            end
        end
    end

    local footerY = y + 4
    setY("MeterFooterDivider", footerY)
    setY("MeterFooterDot", footerY + 17)
    setY("MeterFooterStatus", footerY + 7)

    setY("MeterFooterSyncPill", footerY + 7)
    setX("MeterFooterSyncBtn", 328)
    setY("MeterFooterSyncBtn", footerY + 5)
    setY("MeterFooterSyncClickZone", footerY + 4)

    updateFooterText()

    show("MeterFooterDivider")
    show("MeterFooterDot")
    show("MeterFooterStatus")
    show("MeterFooterSyncPill")
    show("MeterFooterSyncBtn")
    show("MeterFooterSyncClickZone")

    y = footerY + G.FOOTER_H

    setOpt("MeterBackground", "Shape",
        "Rectangle 0,0," .. G.SKIN_W .. "," .. y .. ",14" ..
        " | Fill Color #ColorBgHud# | StrokeWidth 0.75 | Stroke Color #ColorBorderHud#")
    SKIN:Bang("!SetVariable", "HUDHeight", tostring(y))

    for _, m in ipairs(OWNED) do
        SKIN:Bang("!UpdateMeter", m)
    end
    SKIN:Bang("!UpdateMeter", "MeterBackground")
    SKIN:Bang("!UpdateMeter", "MeterHeaderTitle")
    SKIN:Bang("!UpdateMeter", "MeterHeaderTopPill")
    SKIN:Bang("!UpdateMeter", "MeterHeaderTopBtn")
    SKIN:Bang("!UpdateMeter", "MeterHeaderThemePill")
    SKIN:Bang("!UpdateMeter", "MeterHeaderThemeBtn")
    SKIN:Bang("!UpdateMeter", "MeterHeaderCollapseBtn")
    SKIN:Bang("!Redraw")
end

function Initialize()
    buildOwned()
    state.theme = SKIN:GetVariable("Theme", "obsidian")
    if not themes[state.theme] then
        state.theme = "obsidian"
        persist("Theme", "obsidian")
    end
    loadChatsFromVars()
    applyTheme()
    layout()
end

function Update()
    updateFooterText()
    local now = os.time()
    if now >= nextPoll then
        nextPoll = now + G.POLL_SEC
        SKIN:Bang("!CommandMeasure", "MeasureContextSync", "Run")
    end
end

function OnContextSync()
    lastSyncTime = os.time()
    nextPoll = os.time() + G.POLL_SEC
    setOpt("MeterFooterSyncBtn", "Text", "#DefaultSyncIcon#")
    loadChatsFromVars()
    layout()
end

function SyncNow()
    lastSyncTime = os.time()
    nextPoll = os.time() + G.POLL_SEC
    setOpt("MeterFooterSyncBtn", "Text", "...")
    SKIN:Bang("!UpdateMeter", "MeterFooterSyncBtn")
    SKIN:Bang("!Redraw")
    SKIN:Bang("!CommandMeasure", "MeasureContextForceSync", "Run")
end

function CycleTopCount()
    local cur = getTopN()
    local nxt = (cur == 3) and 5 or ((cur == 5) and 8 or 3)
    persist("TopChatCount", nxt)
    layout()
end

function ToggleCollapsed()
    persist("Collapsed", isCollapsed() and "0" or "1")
    layout()
end

function ToggleTheme()
    local nextTheme = (state.theme == "obsidian") and "frost" or "obsidian"
    state.theme = nextTheme
    persist("Theme", nextTheme)
    applyTheme()
    layout()
end

function SetTheme(name)
    local n = tostring(name or ""):lower()
    if themes[n] then
        state.theme = n
        persist("Theme", n)
        applyTheme()
        layout()
    end
end

function AdjustAlpha(delta)
    local a = tonumber(SKIN:GetVariable("WidgetAlpha", "235")) or 235
    a = math.max(40, math.min(255, a + (tonumber(delta) or 0)))
    persist("WidgetAlpha", a)
    applyTheme()
    layout()
end
