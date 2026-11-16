import { beforeMount } from '@playwright/experimental-ct-react/hooks';
import { MemoryRouter } from 'react-router-dom';
import '../src/index.css';

type HooksConfig = {
  route?: string;
};

beforeMount<HooksConfig>(async ({ App, hooksConfig }) => {
  return (
    <MemoryRouter initialEntries={[hooksConfig?.route ?? '/']}>
      <App />
    </MemoryRouter>
  );
});
