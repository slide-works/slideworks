import { test, expect } from '@playwright/experimental-ct-react';
import { Avatar } from './Avatar';

test('avatar shows the picture and not the initials when there is a picture', async ({
  mount,
  page,
}) => {
  await mount(
    <Avatar avatar={{ initials: 'SR', colour: 'teal', pictureUrl: '/api/people/x/photo/y' }} />,
  );

  await expect(page.getByTestId('avatar-picture')).toHaveAttribute('src', '/api/people/x/photo/y');
  await expect(page.getByTestId('avatar-initials')).toHaveCount(0);
});

test('avatar shows the initials on the colour and no picture when there is none', async ({
  mount,
  page,
}) => {
  await mount(<Avatar avatar={{ initials: 'SR', colour: 'teal', pictureUrl: null }} />);

  await expect(page.getByTestId('avatar-initials')).toHaveText('SR');
  await expect(page.getByTestId('avatar')).toHaveAttribute('data-colour', 'teal');
  await expect(page.getByTestId('avatar')).toHaveCSS('background-color', 'rgb(211, 238, 240)');
  await expect(page.getByTestId('avatar-picture')).toHaveCount(0);
});
