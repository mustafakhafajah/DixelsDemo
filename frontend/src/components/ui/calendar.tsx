import * as React from 'react'
import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react'
import { DayPicker, getDefaultClassNames, type Numerals } from 'react-day-picker'
import { useTranslation } from 'react-i18next'

import { languageOf } from '@/i18n/languages'
import { cn } from '@/lib/utils'

/* The digits Intl uses for the language, so the calendar matches the date written in its field. */
const numeralsOf = (locale: string) => new Intl.NumberFormat(locale).resolvedOptions().numberingSystem as Numerals

/* shadcn/ui Calendar (react-day-picker), without the library's stylesheet: every part is styled here.
 * Month and weekday names, digits and direction follow the chosen language. */
function Calendar({ className, classNames, showOutsideDays = true, components, ...props }: React.ComponentProps<typeof DayPicker>) {
  const d = getDefaultClassNames()
  const { i18n } = useTranslation()
  const language = languageOf(i18n.language)
  const navBtn = 'tw:inline-flex tw:size-8 tw:items-center tw:justify-center tw:rounded-md tw:border-0 tw:bg-transparent tw:p-0 tw:text-foreground tw:cursor-pointer tw:hover:bg-accent tw:hover:text-accent-foreground tw:disabled:opacity-40 tw:disabled:cursor-default'
  return (
    <DayPicker
      showOutsideDays={showOutsideDays}
      locale={language.calendar}
      numerals={numeralsOf(language.locale)}
      dir={i18n.dir()}
      className={cn('tw:w-fit tw:select-none', className)}
      classNames={{
        root: cn('tw:w-fit', d.root),
        months: cn('tw:relative tw:flex tw:flex-col tw:gap-4', d.months),
        month: cn('tw:flex tw:w-full tw:flex-col tw:gap-3', d.month),
        nav: cn('tw:absolute tw:inset-x-0 tw:top-0 tw:flex tw:w-full tw:items-center tw:justify-between', d.nav),
        button_previous: cn(navBtn, d.button_previous),
        button_next: cn(navBtn, d.button_next),
        month_caption: cn('tw:flex tw:h-8 tw:w-full tw:items-center tw:justify-center tw:px-8', d.month_caption),
        caption_label: cn('tw:text-sm tw:font-semibold', d.caption_label),
        month_grid: cn('tw:w-full tw:border-collapse', d.month_grid),
        weekdays: cn('tw:flex', d.weekdays),
        weekday: cn('tw:flex-1 tw:rounded-md tw:text-[0.75rem] tw:font-medium tw:text-muted-foreground tw:select-none', d.weekday),
        week: cn('tw:mt-1 tw:flex tw:w-full', d.week),
        day: cn('tw:relative tw:aspect-square tw:w-9 tw:p-0 tw:text-center tw:select-none', d.day),
        day_button: cn(
          'tw:inline-flex tw:size-9 tw:items-center tw:justify-center tw:rounded-md tw:border-0 tw:bg-transparent tw:p-0 tw:text-sm tw:font-normal tw:text-foreground tw:cursor-pointer',
          'tw:hover:bg-accent tw:hover:text-accent-foreground tw:focus-visible:outline-2 tw:focus-visible:outline-ring',
          d.day_button,
        ),
        selected: cn('tw:[&>button]:bg-primary tw:[&>button]:text-primary-foreground tw:[&>button]:font-semibold tw:[&>button:hover]:bg-primary tw:[&>button:hover]:text-primary-foreground', d.selected),
        today: cn('tw:[&>button]:ring-1 tw:[&>button]:ring-ring', d.today),
        outside: cn('tw:[&>button]:text-muted-foreground tw:[&>button]:opacity-60', d.outside),
        disabled: cn('tw:[&>button]:text-muted-foreground tw:[&>button]:opacity-40 tw:[&>button]:cursor-not-allowed tw:[&>button:hover]:bg-transparent', d.disabled),
        hidden: cn('tw:invisible', d.hidden),
        ...classNames,
      }}
      components={{
        /* Previous / next always ask for left / right; in right-to-left languages they point the other way. */
        Chevron: ({ orientation, className: c }) =>
          orientation === 'left' ? <ChevronLeftIcon className={cn('tw:size-4 flip-rtl', c)} /> : <ChevronRightIcon className={cn('tw:size-4 flip-rtl', c)} />,
        ...components,
      }}
      {...props}
    />
  )
}

export { Calendar }
