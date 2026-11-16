import { Outlet } from 'react-router-dom';

// The layout every page renders inside: header, then the routed page.
export function Shell() {
  return (
    <>
      <header className="sticky top-0 z-10 border-b border-edge bg-panel/90 backdrop-blur">
        <div className="mx-auto flex w-full max-w-4xl items-baseline gap-3 px-4 py-3 sm:px-6">
          <h1 className="m-0 text-base font-bold tracking-tight text-ink">
            SlideWorks<span className="text-signal">.</span>
          </h1>
        </div>
      </header>
      <main className="mx-auto w-full max-w-4xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>
    </>
  );
}
