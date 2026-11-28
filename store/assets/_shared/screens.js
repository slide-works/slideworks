// The v0 screens drawn as HTML, from the same tokens as the app. Returns the markup of one phone.
window.SW = window.SW || {};
window.SW.screens = {
  home: () => `<h1 style="margin-top:calc(var(--u) * 14); font-size:calc(var(--u) * 24)">Join a talk</h1>
    <div class="code" style="margin-top:calc(var(--u) * 16)">482917</div>
    <p class="hint">Enter the six-digit code.</p>
    <div class="btn">Join a talk</div>
    <div class="btn alt">Sign in</div>`,
  join: () => `<h1 style="margin-top:calc(var(--u) * 14); font-size:calc(var(--u) * 24)">Q4 Planning Review</h1>
    <p class="live">Slide 7 of 24</p>`,
  talks: () => `<div class="nav"><h1 style="font-size:calc(var(--u) * 34)">Talks</h1><div class="avatar">JD</div></div>
    <div style="margin-top:calc(var(--u) * 14)"></div>
    ${[['Q4 Planning Review', '2 days ago'], ['Onboarding Kickoff', '5 days ago'], ['Design Systems Tour', 'last week'], ['Team Offsite Recap', '2 weeks ago'], ['Launch Retro', '3 weeks ago'], ['Roadmap Walkthrough', 'last month']]
      .map(([t, u]) => `<div class="row"><b>${t}</b><span>Updated ${u}</span></div>`).join('')}`,
  talk: () => `<div class="nav"><div class="brand" style="margin:0">${SW.mark('dark', 34)}SlideWorks</div></div>
    <div class="pill">Connected</div><h1 style="margin-top:calc(var(--u) * 14)">Q4 Planning Review</h1>
    <p class="sub">Move the slide on the web and this number follows.</p>
    <div class="big"><small>Current slide</small><strong>12</strong><p>Updates live</p></div>`,
  signin: () => `<div class="label" style="margin-top:calc(var(--u) * 14)">Server</div>
    <div class="server">https://slideworks.app<em>Use another server</em></div>
    <div class="btn">Sign in with Google</div>`,
};
// An iPhone frame (Dynamic Island) unless the page's body says data-frame="android" (a punch-hole camera).
window.SW.phone = (name, dark) => `<div class="phone-box"><div class="phone${dark ? ' dark' : ''}${document.body.dataset.frame === 'android' ? ' android' : ''}"><div class="${document.body.dataset.frame === 'android' ? 'punch' : 'island'}"></div>
  <div class="status"><span>9:41</span><i></i></div><div class="screen">${SW.screens[name]()}</div></div></div>`;
