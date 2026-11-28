// The v0 screens drawn as HTML, from the same tokens as the app. Returns the markup of one phone.
window.SW = window.SW || {};
window.SW.screens = {
  home: () => `<div class="brand">${SW.mark('light', 34)}SlideWorks</div>
    <h1 style="margin-top:calc(var(--u) * 34)">Join a talk</h1><p class="sub">Type the six-digit code shown on the screen.</p>
    <div class="label">Code</div>
    <div class="digits"><b>4</b><b>8</b><b>2</b><b>9</b><b>1</b><b class="on">7</b></div>
    <div class="btn" style="margin-top:calc(var(--u) * 22)">Join</div>
    <div class="or">or</div><div class="btn alt">Sign in</div>
    <p class="foot">No account needed to join a talk.</p>`,
  join: () => `<div class="nav"><div class="brand" style="margin:0">${SW.mark('light', 34)}SlideWorks</div></div>
    <div class="pill">Live</div><h1 style="margin-top:calc(var(--u) * 14)">Q4 Planning Review</h1>
    <p class="sub">You joined with code 482 917.</p>
    <div class="big"><small>Slide</small><strong>7</strong><p>Follows the speaker live</p></div>`,
  talks: () => `<div class="nav"><h1 style="font-size:calc(var(--u) * 34)">Talks</h1><div class="avatar">JD</div></div>
    <div style="margin-top:calc(var(--u) * 14)"></div>
    ${[['Q4 Planning Review', '2 days ago'], ['Onboarding Kickoff', '5 days ago'], ['Design Systems Tour', 'last week'], ['Team Offsite Recap', '2 weeks ago'], ['Launch Retro', '3 weeks ago'], ['Roadmap Walkthrough', 'last month']]
      .map(([t, u]) => `<div class="row"><b>${t}</b><span>Updated ${u}</span></div>`).join('')}`,
  talk: () => `<div class="nav"><div class="brand" style="margin:0">${SW.mark('dark', 34)}SlideWorks</div></div>
    <div class="pill">Connected</div><h1 style="margin-top:calc(var(--u) * 14)">Q4 Planning Review</h1>
    <p class="sub">Move the slide on the web and this number follows.</p>
    <div class="big"><small>Current slide</small><strong>12</strong><p>Updates live</p></div>`,
  signin: () => `<div class="brand">${SW.mark('light', 34)}SlideWorks</div>
    <h1 style="margin-top:calc(var(--u) * 34)">Sign in</h1><p class="sub">Use the hosted server, or type the address of your own.</p>
    <div class="label">Server</div>
    <div class="field">https://slideworks.app<em>Reachable</em></div>
    <div class="btn" style="margin-top:calc(var(--u) * 22)">Sign in with Google</div>
    <p class="foot">You sign in through your browser. The app never sees your Google password.</p>`,
};
window.SW.phone = (name, dark) => `<div class="phone-box"><div class="phone${dark ? ' dark' : ''}"><div class="island"></div>
  <div class="status"><span>9:41</span><i></i></div><div class="screen">${SW.screens[name]()}</div></div></div>`;
